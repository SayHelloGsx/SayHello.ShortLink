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
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.ShortLink.ShortLinks;

public class EfCoreShortLinkRepository :
    EfCoreRepository<IShortLinkDbContext, ShortLink, Guid>,
    IShortLinkRepository
{
    private readonly IDataFilter<ISoftDelete> _softDeleteFilter;
    private readonly IDataFilter<IMultiTenant> _multiTenantFilter;

    public EfCoreShortLinkRepository(
        IDbContextProvider<IShortLinkDbContext> dbContextProvider,
        IDataFilter<ISoftDelete> softDeleteFilter,
        IDataFilter<IMultiTenant> multiTenantFilter)
        : base(dbContextProvider)
    {
        _softDeleteFilter = softDeleteFilter;
        _multiTenantFilter = multiTenantFilter;
    }

    public virtual async Task<bool> CodeExistsAsync(
        string origin,
        string pathNamespace,
        string code,
        CancellationToken cancellationToken = default)
    {
        using (_softDeleteFilter.Disable())
        using (_multiTenantFilter.Disable())
        {
            return await (await GetDbSetAsync()).AnyAsync(
                x => x.Origin == origin && x.Namespace == pathNamespace && x.Code == code,
                GetCancellationToken(cancellationToken));
        }
    }

    public virtual Task<bool> CodeExistsAsync(
        string origin,
        string code,
        CancellationToken cancellationToken = default) =>
        CodeExistsAsync(origin, string.Empty, code, cancellationToken);

    public virtual async Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        using (_softDeleteFilter.Disable())
        using (_multiTenantFilter.Disable())
        {
            return await (await GetDbSetAsync()).AnyAsync(
                x => x.Origin == null && x.Namespace == string.Empty && x.Code == code,
                GetCancellationToken(cancellationToken));
        }
    }

    public virtual async Task<ShortLink?> FindByCodeAsync(
        string origin,
        string pathNamespace,
        string code,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        using (_multiTenantFilter.Disable())
        {
            if (includeDeleted)
            {
                using (_softDeleteFilter.Disable())
                {
                    return await FindByCodeInternalAsync(origin, pathNamespace, code, cancellationToken);
                }
            }

            return await FindByCodeInternalAsync(origin, pathNamespace, code, cancellationToken);
        }
    }

    public virtual Task<ShortLink?> FindByCodeAsync(
        string origin,
        string code,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default) =>
        FindByCodeAsync(origin, string.Empty, code, includeDeleted, cancellationToken);

    public virtual async Task<ShortLink?> FindLegacyByCodeAsync(
        string code,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        using (_multiTenantFilter.Disable())
        {
            if (includeDeleted)
            {
                using (_softDeleteFilter.Disable())
                {
                    return await FindLegacyByCodeInternalAsync(code, cancellationToken);
                }
            }

            return await FindLegacyByCodeInternalAsync(code, cancellationToken);
        }
    }

    public virtual Task<ShortLink?> FindByCodeAsync(
        string code,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default) =>
        FindLegacyByCodeAsync(code, includeDeleted, cancellationToken);

    // Quota counts keep an explicit IsDeleted predicate so soft-deleted links never consume quota,
    // even when a caller has disabled the ISoftDelete data filter.
    public virtual async Task<long> GetCountByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).LongCountAsync(
            x => x.OwnerUserId == ownerUserId && !x.IsDeleted,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetRootLinkCountByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).LongCountAsync(
            x => x.OwnerUserId == ownerUserId && x.Namespace == string.Empty && !x.IsDeleted,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetCustomCodeCountByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).LongCountAsync(
            x => x.OwnerUserId == ownerUserId && x.IsCustomCode && !x.IsDeleted,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<ShortLink>> GetListAsync(
        Guid? ownerUserId,
        string? filter,
        ShortLinkStatus? status,
        string? sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await CreateFilteredQueryAsync(ownerUserId, filter, status);

        return await ApplySorting(query, sorting)
            .AsNoTracking()
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetCountAsync(
        Guid? ownerUserId,
        string? filter,
        ShortLinkStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = await CreateFilteredQueryAsync(ownerUserId, filter, status);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task RecordVisitAsync(
        ShortLinkVisit visit,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        await dbContext.Set<ShortLinkVisit>().AddAsync(
            visit,
            GetCancellationToken(cancellationToken));

        var affectedRows = await dbContext.Set<ShortLink>()
            .IgnoreQueryFilters()
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

    public virtual async Task<long> BackfillDomainAsync(
        Guid domainId,
        string origin,
        CancellationToken cancellationToken = default)
    {
        using (_softDeleteFilter.Disable())
        {
            return await (await GetDbSetAsync())
                .Where(x => x.DomainId == null && x.Origin == null &&
                            x.Namespace == string.Empty && x.NamespaceId == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.DomainId, domainId)
                        .SetProperty(x => x.Origin, origin),
                    GetCancellationToken(cancellationToken));
        }
    }

    private async Task<ShortLink?> FindByCodeInternalAsync(
        string origin,
        string pathNamespace,
        string code,
        CancellationToken cancellationToken)
    {
        return await (await GetDbSetAsync()).FirstOrDefaultAsync(
            x => x.Origin == origin && x.Namespace == pathNamespace && x.Code == code,
            GetCancellationToken(cancellationToken));
    }

    private async Task<ShortLink?> FindLegacyByCodeInternalAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return await (await GetDbSetAsync()).FirstOrDefaultAsync(
            x => x.Origin == null && x.Namespace == string.Empty && x.Code == code,
            GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<ShortLink>> CreateFilteredQueryAsync(
        Guid? ownerUserId,
        string? filter,
        ShortLinkStatus? status)
    {
        var query = (await GetDbSetAsync()).AsQueryable();

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
        IOrderedQueryable<ShortLink> ordered = sorting?.Trim().ToLowerInvariant() switch
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

        // None of the sort keys is unique, so the Id tie-breaker keeps Skip/Take pages stable.
        return ordered.ThenBy(x => x.Id);
    }
}
