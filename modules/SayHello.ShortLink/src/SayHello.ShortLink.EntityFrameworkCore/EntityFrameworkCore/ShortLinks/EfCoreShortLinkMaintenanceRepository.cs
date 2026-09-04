using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;

namespace SayHello.ShortLink.ShortLinks;

public class EfCoreShortLinkMaintenanceRepository :
    IShortLinkMaintenanceRepository,
    ITransientDependency
{
    private readonly IDbContextProvider<IShortLinkDbContext> _dbContextProvider;
    private readonly IDataFilter<ISoftDelete> _softDeleteFilter;
    private readonly IGuidGenerator _guidGenerator;

    public EfCoreShortLinkMaintenanceRepository(
        IDbContextProvider<IShortLinkDbContext> dbContextProvider,
        IDataFilter<ISoftDelete> softDeleteFilter,
        IGuidGenerator guidGenerator)
    {
        _dbContextProvider = dbContextProvider;
        _softDeleteFilter = softDeleteFilter;
        _guidGenerator = guidGenerator;
    }

    public async Task ArchiveVisitsBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        var visits = dbContext.ShortLinkVisits.Where(x => x.VisitedAt < cutoff);

        var dailyAggregates = await visits
            .GroupBy(x => new { x.ShortLinkId, x.TenantId, Date = x.VisitedAt.Date })
            .Select(group => new DailyAggregate(
                group.Key.ShortLinkId,
                group.Key.TenantId,
                group.Key.Date,
                group.LongCount(),
                group.Select(x => x.VisitorHash).Distinct().LongCount()))
            .ToListAsync(cancellationToken);

        if (dailyAggregates.Count == 0)
        {
            return;
        }

        var dates = dailyAggregates
            .Select(x => DateOnly.FromDateTime(x.Date))
            .Distinct()
            .ToList();
        var linkIds = dailyAggregates.Select(x => x.ShortLinkId).Distinct().ToList();

        var existingDaily = await dbContext.ShortLinkDailyStatistics
            .Where(x => linkIds.Contains(x.ShortLinkId) && dates.Contains(x.Date))
            .ToListAsync(cancellationToken);
        var dailyByKey = existingDaily.ToDictionary(x => (x.ShortLinkId, x.Date));

        foreach (var aggregate in dailyAggregates)
        {
            var date = DateOnly.FromDateTime(aggregate.Date);
            if (dailyByKey.TryGetValue((aggregate.ShortLinkId, date), out var existing))
            {
                existing.SetCounts(aggregate.VisitCount, aggregate.UniqueVisitorCount);
            }
            else
            {
                dbContext.ShortLinkDailyStatistics.Add(
                    new ShortLinkDailyStatistic(
                        _guidGenerator.Create(),
                        aggregate.TenantId,
                        aggregate.ShortLinkId,
                        date,
                        aggregate.VisitCount,
                        aggregate.UniqueVisitorCount));
            }
        }

        var referrers = await visits
            .GroupBy(x => new
            {
                x.ShortLinkId,
                x.TenantId,
                Date = x.VisitedAt.Date,
                Value = x.ReferrerHost ?? "Direct"
            })
            .Select(group => new DimensionAggregate(
                group.Key.ShortLinkId,
                group.Key.TenantId,
                group.Key.Date,
                group.Key.Value,
                group.LongCount()))
            .ToListAsync(cancellationToken);
        var browsers = await visits
            .GroupBy(x => new { x.ShortLinkId, x.TenantId, Date = x.VisitedAt.Date, Value = x.Browser })
            .Select(group => new DimensionAggregate(
                group.Key.ShortLinkId,
                group.Key.TenantId,
                group.Key.Date,
                group.Key.Value,
                group.LongCount()))
            .ToListAsync(cancellationToken);
        var devices = await visits
            .GroupBy(x => new { x.ShortLinkId, x.TenantId, Date = x.VisitedAt.Date, Value = x.DeviceType })
            .Select(group => new DimensionAggregate(
                group.Key.ShortLinkId,
                group.Key.TenantId,
                group.Key.Date,
                group.Key.Value,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        await UpsertDimensionsAsync(
            dbContext,
            referrers,
            StatisticDimension.ReferrerHost,
            cancellationToken);
        await UpsertDimensionsAsync(
            dbContext,
            browsers,
            StatisticDimension.Browser,
            cancellationToken);
        await UpsertDimensionsAsync(
            dbContext,
            devices,
            StatisticDimension.DeviceType,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await visits.ExecuteDeleteAsync(cancellationToken);
    }

    public async Task PurgeDeletedLinksBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        using (_softDeleteFilter.Disable())
        {
            await dbContext.ShortLinks
                .Where(x => x.IsDeleted && x.DeletionTime.HasValue && x.DeletionTime.Value < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task UpsertDimensionsAsync(
        IShortLinkDbContext dbContext,
        IReadOnlyCollection<DimensionAggregate> aggregates,
        StatisticDimension dimension,
        CancellationToken cancellationToken)
    {
        if (aggregates.Count == 0)
        {
            return;
        }

        var dates = aggregates.Select(x => DateOnly.FromDateTime(x.Date)).Distinct().ToList();
        var linkIds = aggregates.Select(x => x.ShortLinkId).Distinct().ToList();
        var existing = await dbContext.ShortLinkDailyDimensionStatistics
            .Where(x => x.Dimension == dimension &&
                        linkIds.Contains(x.ShortLinkId) &&
                        dates.Contains(x.Date))
            .ToListAsync(cancellationToken);
        var byKey = existing.ToDictionary(x => (x.ShortLinkId, x.Date, x.Value));

        foreach (var aggregate in aggregates)
        {
            var date = DateOnly.FromDateTime(aggregate.Date);
            if (byKey.TryGetValue((aggregate.ShortLinkId, date, aggregate.Value), out var item))
            {
                item.SetVisitCount(aggregate.VisitCount);
            }
            else
            {
                dbContext.ShortLinkDailyDimensionStatistics.Add(
                    new ShortLinkDailyDimensionStatistic(
                        _guidGenerator.Create(),
                        aggregate.TenantId,
                        aggregate.ShortLinkId,
                        date,
                        dimension,
                        aggregate.Value,
                        aggregate.VisitCount));
            }
        }
    }

    private sealed record DailyAggregate(
        Guid ShortLinkId,
        Guid? TenantId,
        DateTime Date,
        long VisitCount,
        long UniqueVisitorCount);

    private sealed record DimensionAggregate(
        Guid ShortLinkId,
        Guid? TenantId,
        DateTime Date,
        string Value,
        long VisitCount);
}
