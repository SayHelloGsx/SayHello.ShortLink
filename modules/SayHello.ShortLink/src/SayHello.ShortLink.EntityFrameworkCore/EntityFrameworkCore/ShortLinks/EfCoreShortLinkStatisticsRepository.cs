using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.ShortLink.ShortLinks;

public class EfCoreShortLinkStatisticsRepository :
    IShortLinkStatisticsRepository,
    ITransientDependency
{
    private readonly IDbContextProvider<IShortLinkDbContext> _dbContextProvider;

    public EfCoreShortLinkStatisticsRepository(
        IDbContextProvider<IShortLinkDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task<ShortLinkStatisticsData> GetAsync(
        Guid shortLinkId,
        DateOnly startDate,
        DateOnly endDate,
        int maxDimensionItems,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate));
        }

        if (maxDimensionItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDimensionItems));
        }

        var dbContext = await _dbContextProvider.GetDbContextAsync();
        var rollups = await dbContext.ShortLinkDailyStatistics
            .AsNoTracking()
            .Where(x =>
                x.ShortLinkId == shortLinkId &&
                x.Date >= startDate &&
                x.Date <= endDate)
            .ToListAsync(cancellationToken);

        var lastRollupDate = rollups.Count == 0
            ? startDate.AddDays(-1)
            : rollups.Max(x => x.Date);
        var rawStart = lastRollupDate
            .AddDays(1)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = endDate
            .AddDays(1)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rawVisits = dbContext.ShortLinkVisits
            .AsNoTracking()
            .Where(x =>
                x.ShortLinkId == shortLinkId &&
                x.VisitedAt >= rawStart &&
                x.VisitedAt < endExclusive);

        var rawDailyRows = await rawVisits
            .GroupBy(x => x.VisitedAt.Date)
            .Select(group => new
            {
                Date = group.Key,
                VisitCount = group.LongCount(),
                UniqueVisitorCount = group
                    .Select(x => x.VisitorHash)
                    .Distinct()
                    .LongCount()
            })
            .ToListAsync(cancellationToken);

        var dailyByDate = rollups.ToDictionary(
            x => x.Date,
            x => new ShortLinkDailyVisitData(
                x.Date,
                x.VisitCount,
                x.UniqueVisitorCount));

        foreach (var row in rawDailyRows)
        {
            var date = DateOnly.FromDateTime(row.Date);
            dailyByDate[date] = new ShortLinkDailyVisitData(
                date,
                row.VisitCount,
                row.UniqueVisitorCount);
        }

        var daily = new List<ShortLinkDailyVisitData>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            daily.Add(
                dailyByDate.GetValueOrDefault(date) ??
                new ShortLinkDailyVisitData(date, 0, 0));
        }

        var referrers = await GetDimensionAsync(
            dbContext,
            rawVisits,
            shortLinkId,
            startDate,
            endDate,
            StatisticDimension.ReferrerHost,
            maxDimensionItems,
            cancellationToken);
        var browsers = await GetDimensionAsync(
            dbContext,
            rawVisits,
            shortLinkId,
            startDate,
            endDate,
            StatisticDimension.Browser,
            maxDimensionItems,
            cancellationToken);
        var devices = await GetDimensionAsync(
            dbContext,
            rawVisits,
            shortLinkId,
            startDate,
            endDate,
            StatisticDimension.DeviceType,
            maxDimensionItems,
            cancellationToken);

        return new ShortLinkStatisticsData(
            daily.Sum(x => x.UniqueVisitorCount),
            daily,
            referrers,
            browsers,
            devices);
    }

    private static async Task<IReadOnlyList<ShortLinkDimensionVisitData>> GetDimensionAsync(
        IShortLinkDbContext dbContext,
        IQueryable<ShortLinkVisit> rawVisits,
        Guid shortLinkId,
        DateOnly startDate,
        DateOnly endDate,
        StatisticDimension dimension,
        int maxDimensionItems,
        CancellationToken cancellationToken)
    {
        var rollupRows = await dbContext.ShortLinkDailyDimensionStatistics
            .AsNoTracking()
            .Where(x =>
                x.ShortLinkId == shortLinkId &&
                x.Date >= startDate &&
                x.Date <= endDate &&
                x.Dimension == dimension)
            .GroupBy(x => x.Value)
            .Select(group => new
            {
                Value = group.Key,
                VisitCount = group.Sum(x => x.VisitCount)
            })
            .ToListAsync(cancellationToken);

        List<ShortLinkDimensionVisitData> raw;
        switch (dimension)
        {
            case StatisticDimension.ReferrerHost:
            {
                var rows = await rawVisits
                    .GroupBy(x => x.ReferrerHost ?? "Direct")
                    .Select(group => new
                    {
                        Value = group.Key,
                        VisitCount = group.LongCount()
                    })
                    .ToListAsync(cancellationToken);
                raw = rows
                    .Select(x => new ShortLinkDimensionVisitData(x.Value, x.VisitCount))
                    .ToList();
                break;
            }
            case StatisticDimension.Browser:
            {
                var rows = await rawVisits
                    .GroupBy(x => x.Browser)
                    .Select(group => new
                    {
                        Value = group.Key,
                        VisitCount = group.LongCount()
                    })
                    .ToListAsync(cancellationToken);
                raw = rows
                    .Select(x => new ShortLinkDimensionVisitData(x.Value, x.VisitCount))
                    .ToList();
                break;
            }
            case StatisticDimension.DeviceType:
            {
                var rows = await rawVisits
                    .GroupBy(x => x.DeviceType)
                    .Select(group => new
                    {
                        Value = group.Key,
                        VisitCount = group.LongCount()
                    })
                    .ToListAsync(cancellationToken);
                raw = rows
                    .Select(x => new ShortLinkDimensionVisitData(x.Value, x.VisitCount))
                    .ToList();
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(dimension));
        }

        return rollupRows
            .Select(x => new ShortLinkDimensionVisitData(x.Value, x.VisitCount))
            .Concat(raw)
            .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ShortLinkDimensionVisitData(
                group.Key,
                group.Sum(x => x.VisitCount)))
            .OrderByDescending(x => x.VisitCount)
            .ThenBy(x => x.Value, StringComparer.Ordinal)
            .Take(maxDimensionItems)
            .ToList();
    }
}
