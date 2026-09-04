using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.ShortLink.ShortLinks;

public class EfCoreShortLinkRepository :
    EfCoreRepository<IShortLinkDbContext, ShortLink, Guid>,
    IShortLinkRepository
{
    private readonly IDataFilter<ISoftDelete> _softDeleteFilter;

    public EfCoreShortLinkRepository(
        IDbContextProvider<IShortLinkDbContext> dbContextProvider,
        IDataFilter<ISoftDelete> softDeleteFilter)
        : base(dbContextProvider)
    {
        _softDeleteFilter = softDeleteFilter;
    }

    public async Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        using (_softDeleteFilter.Disable())
        {
            return await (await GetDbSetAsync()).AnyAsync(
                x => x.Code == code,
                GetCancellationToken(cancellationToken));
        }
    }

    public async Task<ShortLink?> FindByCodeAsync(
        string code,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        if (includeDeleted)
        {
            using (_softDeleteFilter.Disable())
            {
                return await FindByCodeInternalAsync(code, cancellationToken);
            }
        }

        return await FindByCodeInternalAsync(code, cancellationToken);
    }

    public async Task<long> GetCountByOwnerAsync(
        Guid ownerUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await GetCountAsync(
            ownerUserId,
            tenantId,
            filter: null,
            status: null,
            cancellationToken);
    }

    public async Task<List<ShortLink>> GetListAsync(
        Guid? ownerUserId,
        Guid? tenantId,
        string? filter,
        ShortLinkStatus? status,
        string? sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await CreateFilteredQueryAsync(ownerUserId, tenantId, filter, status);

        return await ApplySorting(query, sorting)
            .AsNoTracking()
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<long> GetCountAsync(
        Guid? ownerUserId,
        Guid? tenantId,
        string? filter,
        ShortLinkStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = await CreateFilteredQueryAsync(ownerUserId, tenantId, filter, status);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task RecordVisitAsync(
        ShortLinkVisit visit,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        await dbContext.Set<ShortLinkVisit>().AddAsync(
            visit,
            GetCancellationToken(cancellationToken));

        var affectedRows = await dbContext.Set<ShortLink>()
            .Where(x => x.Id == visit.ShortLinkId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.TotalVisitCount,
                    x => x.TotalVisitCount + 1),
                GetCancellationToken(cancellationToken));

        if (affectedRows != 1)
        {
            throw new EntityNotFoundException(typeof(ShortLink), visit.ShortLinkId);
        }

        await dbContext.SaveChangesAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<ShortLink?> FindByCodeInternalAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return await (await GetDbSetAsync()).FirstOrDefaultAsync(
            x => x.Code == code,
            GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<ShortLink>> CreateFilteredQueryAsync(
        Guid? ownerUserId,
        Guid? tenantId,
        string? filter,
        ShortLinkStatus? status)
    {
        var query = (await GetDbSetAsync()).Where(x => x.TenantId == tenantId);

        if (ownerUserId.HasValue)
        {
            query = query.Where(x => x.OwnerUserId == ownerUserId.Value);
        }

        if (!filter.IsNullOrWhiteSpace())
        {
            var normalizedFilter = filter.Trim();
            query = query.Where(x =>
                x.Code.Contains(normalizedFilter) ||
                x.TargetUrl.Contains(normalizedFilter) ||
                (x.Title != null && x.Title.Contains(normalizedFilter)));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return query;
    }

    private static IQueryable<ShortLink> ApplySorting(
        IQueryable<ShortLink> query,
        string? sorting)
    {
        return sorting?.Trim().ToLowerInvariant() switch
        {
            "code" => query.OrderBy(x => x.Code),
            "code desc" => query.OrderByDescending(x => x.Code),
            "title" => query.OrderBy(x => x.Title),
            "title desc" => query.OrderByDescending(x => x.Title),
            "totalvisitcount" => query.OrderBy(x => x.TotalVisitCount),
            "totalvisitcount desc" => query.OrderByDescending(x => x.TotalVisitCount),
            "creationtime" => query.OrderBy(x => x.CreationTime),
            _ => query.OrderByDescending(x => x.CreationTime)
        };
    }
}
