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

public class EfCoreSubscriptionProductRepository : SubscriptionEfRepository<SubscriptionProduct>, ISubscriptionProductRepository
{
    public EfCoreSubscriptionProductRepository(IDbContextProvider<ISubscriptionDbContext> provider, ICurrentTenant tenant)
        : base(provider, tenant) { }

    public async Task<SubscriptionProduct?> FindByCodeAsync(Guid? tenantId, string code, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        code = SubscriptionCode.Normalize(code);
        return await (await GetDbSetAsync()).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionProduct>> GetByIdsAsync(Guid? tenantId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        return await (await GetDbSetAsync()).AsNoTracking().Where(x => x.TenantId == tenantId && ids.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionPage<SubscriptionProduct>> GetPageAsync(SubscriptionCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(query.TenantId);
        query.Validate();
        var data = (await GetDbSetAsync()).Where(x => x.TenantId == query.TenantId);
        if (query.ProductId.HasValue) data = data.Where(x => x.Id == query.ProductId);
        if (query.State.HasValue) data = data.Where(x => x.State == query.State);
        if (query.PublishedOnly) data = data.Where(x => x.State == SubscriptionCatalogState.Published);
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            var filter = query.Filter.Trim().ToLowerInvariant();
            data = data.Where(x => x.Name.ToLower().Contains(filter) || x.Code.Contains(filter));
        }

        var count = await data.LongCountAsync(cancellationToken);
        var items = await data.SortCatalog(query.Sorting).AsNoTracking().Skip(query.SkipCount)
            .Take(query.MaxResultCount).ToListAsync(cancellationToken);
        return new SubscriptionPage<SubscriptionProduct>(count, items);
    }

    public async Task<bool> IsReferencedAsync(Guid? tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        var db = await GetDbContextAsync();
        return await db.SubscriptionPlans.AnyAsync(x => x.TenantId == tenantId && x.ProductId == productId, cancellationToken) ||
            await db.UserSubscriptions.AnyAsync(x => x.TenantId == tenantId && x.ProductId == productId, cancellationToken);
    }
}
