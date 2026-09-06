using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Entitlements;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace SayHello.Subscription.EntityFrameworkCore;

public class SubscriptionCatalogTests : SubscriptionPersistenceTestBase
{
    [Fact]
    public async Task Database_optimistic_concurrency_rejects_a_detached_stale_write()
    {
        var data = await SeedAsync();
        var repository = GetRequiredService<ISubscriptionProductRepository>();
        var stale = await InTransactionAsync(() => repository.GetAsync(data.Products[0].Id));
        await InTransactionAsync(() => Catalog.UpdateProductAsync(null, stale.Id, stale.ConcurrencyStamp, new CatalogDetails("Winner")));
        stale.UpdateDetails("Stale", null, 0);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => InTransactionAsync(() => repository.UpdateAsync(stale, true)));
        Assert.Equal(SubscriptionErrorCodes.ConcurrencyConflict, exception.Code);
        var persisted = await InTransactionAsync(() => repository.GetAsync(stale.Id));
        Assert.Equal("Winner", persisted.Name);
    }

    [Fact]
    public async Task Explicit_repository_tenant_filters_remain_effective_when_ABP_filter_is_disabled()
    {
        var host = await SeedAsync();
        var tenantId = Guid.NewGuid();
        var tenant = await SeedAsync(tenantId);
        using (GetRequiredService<IDataFilter<IMultiTenant>>().Disable())
        {
            await InTransactionAsync(async () =>
            {
                var repository = GetRequiredService<ISubscriptionProductRepository>();
                Assert.Equal(3, (await repository.GetPageAsync(new SubscriptionCatalogQuery(null))).TotalCount);
                Assert.Empty(await repository.GetByIdsAsync(null, new[] { tenant.Products[0].Id }));
                await Assert.ThrowsAsync<EntityNotFoundException>(() => repository.GetAsync(tenant.Products[0].Id));
                Assert.Equal(host.Products[0].Id, (await repository.FindByCodeAsync(null, "alpha"))!.Id);
                return true;
            });
        }
    }

    [Fact]
    public async Task Published_catalog_filters_ancestors_materializes_children_and_validates_paging()
    {
        var data = await SeedAsync();
        await InTransactionAsync(async () =>
        {
            var plans = await GetRequiredService<ISubscriptionPlanRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(null, PublishedOnly: true, Sorting: SubscriptionCatalogSort.NameDescending, MaxResultCount: 2));
            Assert.Equal(3, plans.TotalCount);
            Assert.Equal(2, plans.Items.Count);
            Assert.All(plans.Items, plan => Assert.Equal(2, plan.Entitlements.Count));
            Assert.Equal("gamma", plans.Items[0].ProductCode);
            await Catalog.SetProductStateAsync(null, data.Products[0].Id, data.Products[0].ConcurrencyStamp, SubscriptionCatalogState.Withdrawn);
            Assert.Equal(2, (await GetRequiredService<ISubscriptionPlanRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(null, PublishedOnly: true))).TotalCount);
            Assert.Equal(0, (await GetRequiredService<ISubscriptionBundleRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(null, PublishedOnly: true))).TotalCount);
            return true;
        });
        Assert.Equal(SubscriptionErrorCodes.InvalidPaging, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => GetRequiredService<ISubscriptionProductRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(null, Sorting: (SubscriptionCatalogSort)99))))).Code);
        Assert.Equal(SubscriptionErrorCodes.InvalidPaging, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => GetRequiredService<ISubscriptionProductRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(null, MaxResultCount: 101))))).Code);
    }

    [Fact]
    public async Task Catalog_withdrawal_keeps_existing_grants_and_history_prevents_deletion()
    {
        var data = await SeedAsync();
        await AssignPlanAsync(data, 0);
        var withdrawn = await InTransactionAsync(() => Catalog.SetProductStateAsync(null, data.Products[0].Id,
            data.Products[0].ConcurrencyStamp, SubscriptionCatalogState.Withdrawn));
        await InTransactionAsync(async () =>
        {
            Assert.True((await GetRequiredService<ISubscriptionEntitlementChecker>().GetBooleanAsync(null, data.UserId, "alpha", "enabled")).IsGranted);
            return true;
        });
        Assert.Equal(SubscriptionErrorCodes.CatalogUnavailable,
            (await Assert.ThrowsAsync<BusinessException>(() => AssignPlanAsync(data, 0))).Code);
        Assert.Equal(SubscriptionErrorCodes.CatalogReferenced, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => DeleteProductAsync(withdrawn.Id, withdrawn.ConcurrencyStamp)))).Code);
    }

    [Fact]
    public async Task Catalog_duplicate_codes_and_stale_updates_fail_explicitly()
    {
        var data = await SeedAsync();
        Assert.Equal(SubscriptionErrorCodes.DuplicateCode, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => Catalog.CreateProductAsync(null, "ALPHA", new CatalogDetails("Duplicate"))))).Code);
        var updated = await InTransactionAsync(() => Catalog.UpdateProductAsync(null, data.Products[0].Id,
            data.Products[0].ConcurrencyStamp, new CatalogDetails("New title")));
        Assert.NotEqual(data.Products[0].ConcurrencyStamp, updated.ConcurrencyStamp);
        Assert.Equal(SubscriptionErrorCodes.ConcurrencyConflict, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => Catalog.UpdateProductAsync(null, updated.Id,
                data.Products[0].ConcurrencyStamp, new CatalogDetails("Stale"))))).Code);
    }

    private async Task<bool> DeleteProductAsync(Guid id, string stamp)
    {
        await Catalog.DeleteProductAsync(null, id, stamp);
        return true;
    }
}
