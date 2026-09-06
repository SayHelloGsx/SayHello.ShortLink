using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.Subscription.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class EfCoreSubscriptionPlanRepository : SubscriptionEfRepository<SubscriptionPlan>, ISubscriptionPlanRepository
{
    public EfCoreSubscriptionPlanRepository(IDbContextProvider<ISubscriptionDbContext> provider, ICurrentTenant tenant)
        : base(provider, tenant) { }

    public override async Task<IQueryable<SubscriptionPlan>> WithDetailsAsync() =>
        (await GetQueryableAsync()).Include(x => x.Entitlements);

    public async Task<SubscriptionPlan?> FindByCodeAsync(Guid? tenantId, Guid productId, string code,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        code = SubscriptionCode.Normalize(code);
        return await (await WithDetailsAsync()).FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.ProductId == productId && x.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetByIdsAsync(Guid? tenantId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return await (await WithDetailsAsync()).AsNoTracking().Where(x => x.TenantId == tenantId && ids.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionPage<SubscriptionPlan>> GetPageAsync(SubscriptionCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(query.TenantId);
        query.Validate();
        var db = await GetDbContextAsync();
        var data = db.SubscriptionPlans.Where(x => x.TenantId == query.TenantId);
        if (query.ProductId.HasValue) data = data.Where(x => x.ProductId == query.ProductId);
        if (query.State.HasValue) data = data.Where(x => x.State == query.State);
        if (query.PublishedOnly)
            data = data.Where(x => x.State == SubscriptionCatalogState.Published &&
                db.SubscriptionProducts.Any(p => p.Id == x.ProductId && p.TenantId == query.TenantId && p.State == SubscriptionCatalogState.Published));
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            var filter = query.Filter.Trim().ToLowerInvariant();
            data = data.Where(x => x.Name.ToLower().Contains(filter) || x.Code.Contains(filter));
        }

        var count = await data.LongCountAsync(cancellationToken);
        var items = await data.SortCatalog(query.Sorting).Include(x => x.Entitlements).AsNoTracking()
            .Skip(query.SkipCount).Take(query.MaxResultCount).ToListAsync(cancellationToken);
        return new SubscriptionPage<SubscriptionPlan>(count, items);
    }

    public async Task<bool> IsReferencedAsync(Guid? tenantId, Guid planId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        var db = await GetDbContextAsync();
        return await db.SubscriptionBundleItems.AnyAsync(x => x.TenantId == tenantId && x.PlanId == planId, cancellationToken) ||
            await db.UserSubscriptions.AnyAsync(x => x.TenantId == tenantId && x.SourcePlanId == planId, cancellationToken);
    }
}
