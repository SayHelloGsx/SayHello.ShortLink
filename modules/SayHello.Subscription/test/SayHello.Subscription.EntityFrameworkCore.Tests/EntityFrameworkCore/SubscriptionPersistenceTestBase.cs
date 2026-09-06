using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace SayHello.Subscription.EntityFrameworkCore;

public abstract class SubscriptionPersistenceTestBase : SubscriptionTestBase<SubscriptionEntityFrameworkCoreTestModule>
{
    protected ISubscriptionCatalogManager Catalog => GetRequiredService<ISubscriptionCatalogManager>();
    protected ISubscriptionManager Manager => GetRequiredService<ISubscriptionManager>();
    protected IUserSubscriptionRepository Subscriptions => GetRequiredService<IUserSubscriptionRepository>();
    protected SubscriptionTestClock TestClock => GetRequiredService<SubscriptionTestClock>();

    protected async Task<T> InTransactionAsync<T>(Func<Task<T>> action, Guid? tenantId = null)
    {
        using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var unit = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        var result = await action();
        await unit.CompleteAsync();
        return result;
    }

    protected async Task<CatalogData> SeedAsync(Guid? tenantId = null)
    {
        var userId = Guid.NewGuid();
        GetRequiredService<SubscriptionTestUserDirectory>().Add(tenantId, userId);
        return await InTransactionAsync(async () =>
        {
            var products = new List<SubscriptionProduct>();
            var plans = new List<SubscriptionPlan>();
            foreach (var code in new[] { "alpha", "beta", "gamma" })
            {
                var product = await Catalog.CreateProductAsync(tenantId, code, new CatalogDetails(code));
                product = await Catalog.SetProductStateAsync(tenantId, product.Id, product.ConcurrencyStamp, SubscriptionCatalogState.Published);
                var plan = await Catalog.CreatePlanAsync(tenantId, product.Id, "basic", new CatalogDetails("Basic " + code),
                    SubscriptionTestDefinitions.Values());
                plan = await Catalog.SetPlanStateAsync(tenantId, plan.Id, plan.ConcurrencyStamp, SubscriptionCatalogState.Published);
                products.Add(product);
                plans.Add(plan);
            }

            var ab = await Catalog.CreateBundleAsync(tenantId, "ab", new CatalogDetails("Alpha and Beta"),
                new[] { plans[0].Id, plans[1].Id });
            ab = await Catalog.SetBundleStateAsync(tenantId, ab.Id, ab.ConcurrencyStamp, SubscriptionCatalogState.Published);
            var ac = await Catalog.CreateBundleAsync(tenantId, "ac", new CatalogDetails("Alpha and Gamma"),
                new[] { plans[0].Id, plans[2].Id });
            ac = await Catalog.SetBundleStateAsync(tenantId, ac.Id, ac.ConcurrencyStamp, SubscriptionCatalogState.Published);
            return new CatalogData(tenantId, userId, products.ToArray(), plans.ToArray(), ab, ac);
        }, tenantId);
    }

    protected Task<UserSubscription> AssignPlanAsync(CatalogData data, int planIndex, DateTime? expiresAt = null) =>
        InTransactionAsync(async () =>
        {
            var preview = await Manager.PreviewPlanAsync(data.TenantId, data.UserId, data.Plans[planIndex].Id);
            return await Manager.AssignPlanAsync(new AssignSubscriptionPlan(data.TenantId, data.UserId, Target(preview.Items[0], expiresAt)));
        }, data.TenantId);

    protected Task<IReadOnlyList<UserSubscription>> AssignBundleAsync(CatalogData data, SubscriptionBundle bundle,
        Func<SubscriptionAssignmentPreviewItem, DateTime?>? expiration = null) =>
        InTransactionAsync(async () =>
        {
            var preview = await Manager.PreviewBundleAsync(data.TenantId, data.UserId, bundle.Id);
            return await Manager.AssignBundleAsync(new AssignSubscriptionBundle(data.TenantId, data.UserId, bundle.Id,
                preview.BundleConcurrencyStamp!, preview.Items.Select(item => Target(item, expiration?.Invoke(item)))));
        }, data.TenantId);

    protected static SubscriptionAssignmentTarget Target(SubscriptionAssignmentPreviewItem item, DateTime? expiresAt = null) =>
        new(item.ProductId, item.PlanId, item.ProductConcurrencyStamp, item.PlanConcurrencyStamp, expiresAt, item.ExpectedCurrent);

    protected sealed record CatalogData(Guid? TenantId, Guid UserId, SubscriptionProduct[] Products,
        SubscriptionPlan[] Plans, SubscriptionBundle AB, SubscriptionBundle AC);
}
