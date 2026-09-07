using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.Subscription.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace SayHello.Subscription.Catalog;

[UnitOfWork]
public class EfCoreDefaultSubscriptionPlanRepository : IDefaultSubscriptionPlanRepository, ITransientDependency
{
    private readonly IDbContextProvider<ISubscriptionDbContext> _provider;
    private readonly ICurrentTenant _tenant;

    public EfCoreDefaultSubscriptionPlanRepository(IDbContextProvider<ISubscriptionDbContext> provider, ICurrentTenant tenant)
    {
        _provider = provider;
        _tenant = tenant;
    }

    public virtual async Task<DefaultSubscriptionPlan?> FindAsync(Guid? tenantId, string productCode,
        CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_tenant.Id, tenantId);
        productCode = SubscriptionCode.Normalize(productCode);
        var db = await _provider.GetDbContextAsync();
        var selected = await (
            from product in db.SubscriptionProducts.AsNoTracking()
            where product.TenantId == tenantId && product.Code == productCode && product.DefaultPlanId != null
            from plan in db.SubscriptionPlans.AsNoTracking().Include(x => x.Entitlements)
                .Where(x => x.TenantId == tenantId && x.ProductId == product.Id && x.Id == product.DefaultPlanId)
                .DefaultIfEmpty()
            select new { Product = product, Plan = plan })
            .AsSingleQuery().SingleOrDefaultAsync(cancellationToken);
        if (selected == null) return null;
        if (selected.Plan == null) throw new BusinessException(SubscriptionErrorCodes.InvalidDefaultPlan);
        return new DefaultSubscriptionPlan(selected.Product, selected.Plan);
    }

    public virtual async Task<SubscriptionPage<DefaultSubscriptionPlan>> GetPageAsync(SubscriptionCatalogQuery query, Guid userId,
        DateTime now, CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_tenant.Id, query.TenantId);
        SubscriptionGuard.Id(userId, nameof(userId));
        SubscriptionGuard.Utc(now);
        query.Validate();
        var db = await _provider.GetDbContextAsync();
        var products = db.SubscriptionProducts.Where(x => x.TenantId == query.TenantId &&
            x.State == SubscriptionCatalogState.Published && x.DefaultPlanId != null);
        if (query.ProductId.HasValue) products = products.Where(x => x.Id == query.ProductId);
        products = products.Where(product => !db.UserSubscriptions.Any(subscription =>
            subscription.TenantId == query.TenantId && subscription.UserId == userId &&
            subscription.ProductId == product.Id && subscription.IsCurrent && subscription.EndedAt == null &&
            subscription.StartsAt <= now && (subscription.ExpiresAt == null || subscription.ExpiresAt > now)));
        var plans = db.SubscriptionPlans.Where(plan => plan.TenantId == query.TenantId &&
            plan.State == SubscriptionCatalogState.Published &&
            products.Any(product => product.DefaultPlanId == plan.Id && product.Id == plan.ProductId));
        if (query.State.HasValue) plans = plans.Where(plan => plan.State == query.State);
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            var filter = query.Filter.Trim().ToLowerInvariant();
            plans = plans.Where(plan => plan.Name.ToLower().Contains(filter) || plan.Code.Contains(filter) ||
                products.Any(product => product.Id == plan.ProductId &&
                    (product.Name.ToLower().Contains(filter) || product.Code.Contains(filter))));
        }
        var count = await plans.LongCountAsync(cancellationToken);
        var selectedPlans = plans.SortCatalog(query.Sorting).Include(x => x.Entitlements).AsNoTracking()
            .Skip(query.SkipCount).Take(query.MaxResultCount);
        var items = await (
            from plan in selectedPlans
            join product in db.SubscriptionProducts.AsNoTracking().Where(x => x.TenantId == query.TenantId)
                on plan.ProductId equals product.Id
            select new DefaultSubscriptionPlan(product, plan))
            .AsSingleQuery().ToListAsync(cancellationToken);
        return new SubscriptionPage<DefaultSubscriptionPlan>(count, items);
    }
}
