using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.ShortLink.ShortLinkDomains;

public class EfCoreShortLinkDomainRepository :
    EfCoreRepository<IShortLinkDbContext, ShortLinkDomain, Guid>,
    IShortLinkDomainRepository
{
    private readonly IDataFilter<IMultiTenant> _multiTenantFilter;

    public EfCoreShortLinkDomainRepository(
        IDbContextProvider<IShortLinkDbContext> dbContextProvider,
        IDataFilter<IMultiTenant> multiTenantFilter)
        : base(dbContextProvider)
    {
        _multiTenantFilter = multiTenantFilter;
    }

    public async Task<List<ShortLinkDomain>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Origin)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<ShortLinkDomain?> FindByOriginAsync(
        string normalizedOrigin,
        bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(
                x => x.Origin == normalizedOrigin &&
                     (!enabledOnly || x.IsEnabled),
                GetCancellationToken(cancellationToken));
    }

    public async Task<ShortLinkDomain?> FindDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(
                x => x.IsDefault && x.IsEnabled,
                GetCancellationToken(cancellationToken));
    }

    public async Task<bool> OriginExistsAsync(
        string normalizedOrigin,
        bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AnyAsync(
            x => x.Origin == normalizedOrigin &&
                 (!enabledOnly || x.IsEnabled),
            GetCancellationToken(cancellationToken));
    }

    public async Task<bool> IsHostConfiguredAsync(
        string normalizedHost,
        CancellationToken cancellationToken = default)
    {
        using (_multiTenantFilter.Disable())
        {
            var origins = await (await GetDbSetAsync())
                .AsNoTracking()
                .Select(x => x.Origin)
                .ToListAsync(GetCancellationToken(cancellationToken));
            return origins.Any(origin =>
                string.Equals(
                    DomainNameNormalizer.Normalize(new Uri(origin).IdnHost),
                    normalizedHost,
                    StringComparison.Ordinal));
        }
    }

    public async Task<long> GetShortLinkReferenceCountAsync(
        Guid domainId,
        CancellationToken cancellationToken = default)
    {
        var shortLinks = (await GetDbContextAsync()).ShortLinks;
        var domainIdProperty = shortLinks.EntityType.FindProperty(
            ShortLinkDomainConsts.ShortLinkDomainIdPropertyName);
        if (domainIdProperty is null)
        {
            return 0;
        }

        var query = shortLinks.IgnoreQueryFilters();
        var token = GetCancellationToken(cancellationToken);
        if (domainIdProperty.ClrType == typeof(Guid))
        {
            return await query.LongCountAsync(
                x => EF.Property<Guid>(
                    x,
                    ShortLinkDomainConsts.ShortLinkDomainIdPropertyName) == domainId,
                token);
        }

        if (domainIdProperty.ClrType == typeof(Guid?))
        {
            return await query.LongCountAsync(
                x => EF.Property<Guid?>(
                    x,
                    ShortLinkDomainConsts.ShortLinkDomainIdPropertyName) == domainId,
                token);
        }

        throw new AbpException(
            $"'{nameof(ShortLink)}.{ShortLinkDomainConsts.ShortLinkDomainIdPropertyName}' " +
            "must be a Guid or nullable Guid.");
    }
}
