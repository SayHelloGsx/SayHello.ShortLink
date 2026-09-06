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

public class EfCoreSubscriptionBundleRepository : SubscriptionEfRepository<SubscriptionBundle>, ISubscriptionBundleRepository
{
    public EfCoreSubscriptionBundleRepository(IDbContextProvider<ISubscriptionDbContext> provider, ICurrentTenant tenant)
        : base(provider, tenant) { }

    public override async Task<IQueryable<SubscriptionBundle>> WithDetailsAsync() =>
        (await GetQueryableAsync()).Include(x => x.Items);

    public async Task<SubscriptionBundle?> FindByCodeAsync(Guid? tenantId, string code, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        code = SubscriptionCode.Normalize(code);
        return await (await WithDetailsAsync()).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionBundle>> GetByIdsAsync(Guid? tenantId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return await (await WithDetailsAsync()).AsNoTracking().Where(x => x.TenantId == tenantId && ids.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionPage<SubscriptionBundle>> GetPageAsync(SubscriptionCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(query.TenantId);
        query.Validate();
        var db = await GetDbContextAsync();
        var data = db.SubscriptionBundles.Where(x => x.TenantId == query.TenantId);
        if (query.ProductId.HasValue) data = data.Where(x => x.Items.Any(i => i.ProductId == query.ProductId));
        if (query.State.HasValue) data = data.Where(x => x.State == query.State);
        if (query.PublishedOnly)
            data = data.Where(x => x.State == SubscriptionCatalogState.Published && x.Items.Count >= 2 &&
                x.Items.All(i =>
                    db.SubscriptionPlans.Any(p => p.Id == i.PlanId && p.TenantId == query.TenantId && p.State == SubscriptionCatalogState.Published) &&
                    db.SubscriptionProducts.Any(p => p.Id == i.ProductId && p.TenantId == query.TenantId && p.State == SubscriptionCatalogState.Published)));
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            var filter = query.Filter.Trim().ToLowerInvariant();
            data = data.Where(x => x.Name.ToLower().Contains(filter) || x.Code.Contains(filter));
        }

        var count = await data.LongCountAsync(cancellationToken);
        var items = await data.SortCatalog(query.Sorting).Include(x => x.Items).AsNoTracking()
            .Skip(query.SkipCount).Take(query.MaxResultCount).ToListAsync(cancellationToken);
        return new SubscriptionPage<SubscriptionBundle>(count, items);
    }

    public async Task<bool> IsReferencedAsync(Guid? tenantId, Guid bundleId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return await (await GetDbContextAsync()).UserSubscriptions.AnyAsync(
            x => x.TenantId == tenantId && x.SourceBundleId == bundleId, cancellationToken);
    }
}
