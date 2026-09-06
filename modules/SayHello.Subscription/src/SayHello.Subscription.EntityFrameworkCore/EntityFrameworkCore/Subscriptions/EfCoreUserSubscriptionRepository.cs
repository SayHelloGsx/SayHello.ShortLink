using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.Subscription.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Subscriptions;

public class EfCoreUserSubscriptionRepository : SubscriptionEfRepository<UserSubscription>, IUserSubscriptionRepository
{
    public EfCoreUserSubscriptionRepository(IDbContextProvider<ISubscriptionDbContext> provider, ICurrentTenant tenant)
        : base(provider, tenant) { }

    public override async Task<IQueryable<UserSubscription>> WithDetailsAsync() =>
        (await GetQueryableAsync()).Include(x => x.Entitlements);

    public Task<UserSubscription> GetAsync(Guid? tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return base.GetAsync(id, includeDetails: true, cancellationToken);
    }

    public async Task<UserSubscription?> FindCurrentAsync(Guid? tenantId, Guid userId, Guid productId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return await (await WithDetailsAsync()).SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.ProductId == productId && x.IsCurrent, cancellationToken);
    }

    public async Task<IReadOnlyList<UserSubscription>> GetCurrentListAsync(Guid? tenantId, Guid userId,
        IReadOnlyCollection<Guid>? productIds = null, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        var data = (await WithDetailsAsync()).Where(x => x.TenantId == tenantId && x.UserId == userId && x.IsCurrent);
        if (productIds != null) data = data.Where(x => productIds.Contains(x.ProductId));
        return await data.OrderBy(x => x.ProductCode).ToListAsync(cancellationToken);
    }

    public async Task<UserSubscription?> FindEffectiveAsync(Guid? tenantId, Guid userId, string productCode, DateTime now,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        SubscriptionGuard.Utc(now);
        productCode = SubscriptionCode.Normalize(productCode);
        return await (await WithDetailsAsync()).AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.ProductCode == productCode &&
                x.IsCurrent && x.EndedAt == null && x.StartsAt <= now && (x.ExpiresAt == null || x.ExpiresAt > now), cancellationToken);
    }

    public async Task<SubscriptionPage<UserSubscription>> GetPageAsync(UserSubscriptionQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(query.TenantId);
        query.Validate();
        var data = (await GetDbSetAsync()).Where(x => x.TenantId == query.TenantId);
        if (query.UserId.HasValue) data = data.Where(x => x.UserId == query.UserId);
        if (query.ProductId.HasValue) data = data.Where(x => x.ProductId == query.ProductId);
        if (query.CurrentOnly) data = data.Where(x => x.IsCurrent);
        data = query.Status switch
        {
            UserSubscriptionStatus.Active => data.Where(x => x.IsCurrent && x.EndedAt == null &&
                x.StartsAt <= query.Now && (x.ExpiresAt == null || x.ExpiresAt > query.Now)),
            UserSubscriptionStatus.Expired => data.Where(x => x.IsCurrent && x.EndedAt == null && x.ExpiresAt <= query.Now),
            UserSubscriptionStatus.Replaced => data.Where(x => x.EndReason == SubscriptionEndReason.Replaced),
            UserSubscriptionStatus.Revoked => data.Where(x => x.EndReason == SubscriptionEndReason.Revoked),
            UserSubscriptionStatus.NotStarted => data.Where(x => x.IsCurrent && x.EndedAt == null && x.StartsAt > query.Now),
            _ => data
        };
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            var filter = query.Filter.Trim().ToLowerInvariant();
            data = data.Where(x => x.ProductCode.Contains(filter) || x.ProductName.ToLower().Contains(filter) ||
                x.PlanCode.Contains(filter) || x.PlanName.ToLower().Contains(filter));
        }

        var count = await data.LongCountAsync(cancellationToken);
        var ordered = query.Sorting switch
        {
            UserSubscriptionSort.StartsAt => data.OrderBy(x => x.StartsAt),
            UserSubscriptionSort.ExpiresAt => data.OrderBy(x => x.ExpiresAt),
            _ => data.OrderByDescending(x => x.StartsAt)
        };
        var items = await ordered.ThenBy(x => x.Id).Include(x => x.Entitlements).AsNoTracking()
            .Skip(query.SkipCount).Take(query.MaxResultCount).ToListAsync(cancellationToken);
        return new SubscriptionPage<UserSubscription>(count, items);
    }
}
