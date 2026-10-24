using System;
using System.Collections.Generic;
using System.Data.Common;
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

    public virtual async Task<List<ShortLinkDomain>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Origin)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<ShortLinkDomain?> FindByOriginAsync(
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

    public virtual async Task<ShortLinkDomain?> FindDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(
                x => x.IsDefault && x.IsEnabled,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> OriginExistsAsync(
        string normalizedOrigin,
        bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AnyAsync(
            x => x.Origin == normalizedOrigin &&
                 (!enabledOnly || x.IsEnabled),
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> IsHostConfiguredAsync(
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

    public virtual async Task<long> GetShortLinkReferenceCountAsync(
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

    public virtual async Task<Dictionary<Guid, long>> GetShortLinkReferenceCountsAsync(
        IReadOnlyCollection<Guid> domainIds,
        CancellationToken cancellationToken = default)
    {
        if (domainIds.Count == 0)
        {
            return new Dictionary<Guid, long>();
        }

        var ids = domainIds.Distinct().ToList();
        var counts = await (await GetDbContextAsync()).ShortLinks
            .IgnoreQueryFilters()
            .Where(x => x.DomainId != null && ids.Contains(x.DomainId.Value))
            .GroupBy(x => x.DomainId!.Value)
            .Select(x => new { DomainId = x.Key, Count = x.LongCount() })
            .ToListAsync(GetCancellationToken(cancellationToken));
        return counts.ToDictionary(x => x.DomainId, x => x.Count);
    }

    public override async Task DeleteAsync(
        ShortLinkDomain entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await base.DeleteAsync(entity, autoSave, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new BusinessException(
                    ShortLinkErrorCodes.DomainInUse,
                    innerException: exception)
                .WithData("Origin", entity.Origin)
                .WithData("ReferenceCount", ">= 1");
        }
    }

    internal static bool IsForeignKeyViolation(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is DbException databaseException &&
                string.Equals(databaseException.SqlState, "23503", StringComparison.Ordinal))
            {
                return true;
            }

            if (current.GetType()
                    .GetProperty("SqliteExtendedErrorCode")
                    ?.GetValue(current) is 787 ||
                current.Message.Contains(
                    "FOREIGN KEY constraint failed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
