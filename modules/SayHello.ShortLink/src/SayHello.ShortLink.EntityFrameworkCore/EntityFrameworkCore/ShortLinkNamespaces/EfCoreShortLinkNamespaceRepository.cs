using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class EfCoreShortLinkNamespaceRepository :
    EfCoreRepository<IShortLinkDbContext, ShortLinkNamespace, Guid>,
    IShortLinkNamespaceRepository
{
    private readonly IDataFilter<IMultiTenant> _multiTenantFilter;
    private readonly IDataFilter<ISoftDelete> _softDeleteFilter;

    public EfCoreShortLinkNamespaceRepository(
        IDbContextProvider<IShortLinkDbContext> dbContextProvider,
        IDataFilter<IMultiTenant> multiTenantFilter,
        IDataFilter<ISoftDelete> softDeleteFilter)
        : base(dbContextProvider)
    {
        _multiTenantFilter = multiTenantFilter;
        _softDeleteFilter = softDeleteFilter;
    }

    public virtual async Task<bool> NameExistsAsync(
        string origin,
        string name,
        CancellationToken cancellationToken = default)
    {
        using (_multiTenantFilter.Disable())
        {
            return await (await GetDbSetAsync()).AnyAsync(
                x => x.Origin == origin && x.Name == name,
                GetCancellationToken(cancellationToken));
        }
    }

    public virtual async Task<bool> NameExistsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        using (_multiTenantFilter.Disable())
        {
            return await (await GetDbSetAsync()).AnyAsync(
                x => x.Name == name,
                GetCancellationToken(cancellationToken));
        }
    }

    public virtual async Task<long> GetCountByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).LongCountAsync(
            x => x.OwnerUserId == ownerUserId,
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<ShortLinkNamespace>> GetListByOwnerAsync(
        Guid ownerUserId,
        string? origin = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(x => x.OwnerUserId == ownerUserId && (origin == null || x.Origin == origin))
            .AsNoTracking()
            .OrderBy(x => x.Origin)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> HasLinkReferencesAsync(
        Guid namespaceId,
        CancellationToken cancellationToken = default)
    {
        using (_multiTenantFilter.Disable())
        using (_softDeleteFilter.Disable())
        {
            return await (await GetDbContextAsync()).ShortLinks.AnyAsync(
                x => x.NamespaceId == namespaceId,
                GetCancellationToken(cancellationToken));
        }
    }

    public virtual async Task<bool> HasDomainReferencesAsync(
        Guid domainId,
        CancellationToken cancellationToken = default)
    {
        using (_multiTenantFilter.Disable())
        {
            return await (await GetDbSetAsync()).AnyAsync(
                x => x.DomainId == domainId,
                GetCancellationToken(cancellationToken));
        }
    }

    public virtual async Task<List<Guid>> GetReferencedDomainIdsAsync(
        IReadOnlyCollection<Guid> domainIds,
        CancellationToken cancellationToken = default)
    {
        if (domainIds.Count == 0)
        {
            return [];
        }

        var ids = domainIds.Distinct().ToList();
        using (_multiTenantFilter.Disable())
        {
            return await (await GetDbSetAsync())
                .Where(x => ids.Contains(x.DomainId))
                .Select(x => x.DomainId)
                .Distinct()
                .ToListAsync(GetCancellationToken(cancellationToken));
        }
    }
}
