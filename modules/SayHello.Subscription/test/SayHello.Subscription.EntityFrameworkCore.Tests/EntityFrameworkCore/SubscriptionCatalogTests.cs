using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;
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
        await InTransactionAsync(() => Catalog.UpdateProductAsync(stale.Id, stale.ConcurrencyStamp, new CatalogDetails("Winner")));
        stale.UpdateDetails("Stale", null, 0);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => InTransactionAsync(() => repository.UpdateAsync(stale, true)));
        Assert.Equal(SubscriptionErrorCodes.ConcurrencyConflict, exception.Code);
        var persisted = await InTransactionAsync(() => repository.GetAsync(stale.Id));
        Assert.Equal("Winner", persisted.Name);
    }

    [Fact]
    public async Task ABP_tenant_filter_scopes_repositories_and_can_be_disabled()
    {
        var host = await SeedAsync();
        var tenantId = Guid.NewGuid();
        var tenant = await SeedAsync(tenantId);

        await InTransactionAsync(async () =>
        {
            var repository = GetRequiredService<ISubscriptionProductRepository>();
            Assert.Equal(3, (await repository.GetPageAsync(new SubscriptionCatalogQuery())).TotalCount);
            Assert.Empty(await repository.GetByIdsAsync(new[] { tenant.Products[0].Id }));
            await Assert.ThrowsAsync<EntityNotFoundException>(() => repository.GetAsync(tenant.Products[0].Id));
            Assert.Equal(host.Products[0].Id, (await repository.FindByCodeAsync("alpha"))!.Id);
            return true;
        });

        await InTransactionAsync(async () =>
        {
            var repository = GetRequiredService<ISubscriptionProductRepository>();
            Assert.Equal(3, (await repository.GetPageAsync(new SubscriptionCatalogQuery())).TotalCount);
            Assert.Empty(await repository.GetByIdsAsync(new[] { host.Products[0].Id }));
            Assert.Equal(tenant.Products[0].Id, (await repository.GetAsync(tenant.Products[0].Id)).Id);
            Assert.Equal(tenant.Products[0].Id, (await repository.FindByCodeAsync("alpha"))!.Id);
            return true;
        }, tenantId);

        using (GetRequiredService<IDataFilter<IMultiTenant>>().Disable())
        {
            await InTransactionAsync(async () =>
            {
                var repository = GetRequiredService<ISubscriptionProductRepository>();
                Assert.Equal(6, (await repository.GetPageAsync(new SubscriptionCatalogQuery())).TotalCount);
                Assert.Equal(2, (await repository.GetByIdsAsync(
                    new[] { host.Products[0].Id, tenant.Products[0].Id })).Count);
                Assert.Equal(tenant.Products[0].Id, (await repository.GetAsync(tenant.Products[0].Id)).Id);
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
                new SubscriptionCatalogQuery(PublishedOnly: true, Sorting: SubscriptionCatalogSort.NameDescending, MaxResultCount: 2));
            Assert.Equal(3, plans.TotalCount);
            Assert.Equal(2, plans.Items.Count);
            Assert.All(plans.Items, plan => Assert.Equal(2, plan.Entitlements.Count));
            Assert.Equal("gamma", plans.Items[0].ProductCode);
            await Catalog.SetProductStateAsync(data.Products[0].Id, data.Products[0].ConcurrencyStamp, SubscriptionCatalogState.Withdrawn);
            Assert.Equal(2, (await GetRequiredService<ISubscriptionPlanRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(PublishedOnly: true))).TotalCount);
            Assert.Equal(0, (await GetRequiredService<ISubscriptionBundleRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(PublishedOnly: true))).TotalCount);
            return true;
        });
        Assert.Equal(SubscriptionErrorCodes.InvalidPaging, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => GetRequiredService<ISubscriptionProductRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(Sorting: (SubscriptionCatalogSort)99))))).Code);
        Assert.Equal(SubscriptionErrorCodes.InvalidPaging, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => GetRequiredService<ISubscriptionProductRepository>().GetPageAsync(
                new SubscriptionCatalogQuery(MaxResultCount: 101))))).Code);
    }

    [Fact]
    public async Task Catalog_withdrawal_keeps_existing_grants_and_history_prevents_deletion()
    {
        var data = await SeedAsync();
        await AssignPlanAsync(data, 0);
        var withdrawn = await InTransactionAsync(() => Catalog.SetProductStateAsync(data.Products[0].Id,
            data.Products[0].ConcurrencyStamp, SubscriptionCatalogState.Withdrawn));
        await InTransactionAsync(async () =>
        {
            Assert.True((await GetRequiredService<ISubscriptionEntitlementChecker>().GetBooleanAsync(data.UserId, "alpha", "enabled")).IsGranted);
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
            InTransactionAsync(() => Catalog.CreateProductAsync("ALPHA", new CatalogDetails("Duplicate"))))).Code);
        var updated = await InTransactionAsync(() => Catalog.UpdateProductAsync(data.Products[0].Id,
            data.Products[0].ConcurrencyStamp, new CatalogDetails("New title")));
        Assert.NotEqual(data.Products[0].ConcurrencyStamp, updated.ConcurrencyStamp);
        Assert.Equal(SubscriptionErrorCodes.ConcurrencyConflict, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => Catalog.UpdateProductAsync(updated.Id,
                data.Products[0].ConcurrencyStamp, new CatalogDetails("Stale"))))).Code);
    }

    [Fact]
    public async Task Catalog_validates_dynamic_options_but_allows_unchanged_stale_values_on_unrelated_edits()
    {
        var data = await SeedAsync();
        var values = SubscriptionTestDefinitions.Values();
        values["tier"] = EntitlementValue.Enum("pro");
        values["regions"] = EntitlementValue.StringSet(new[] { "us", "apac" });
        var updated = await InTransactionAsync(() => Catalog.UpdatePlanAsync(
            data.Plans[0].Id, data.Plans[0].ConcurrencyStamp, new CatalogDetails("Typed"), values));

        GetRequiredService<SubscriptionTestEntitlementOptionProvider>()
            .SetOptions("tier", new[] { "starter" });
        GetRequiredService<SubscriptionTestEntitlementOptionProvider>()
            .SetOptions("regions", new[] { "eu", "us" });
        values["enabled"] = EntitlementValue.Boolean(false);
        updated = await InTransactionAsync(() => Catalog.UpdatePlanAsync(
            updated.Id, updated.ConcurrencyStamp, new CatalogDetails("Unrelated edit"), values));
        Assert.False(updated.Entitlements.Single(value => value.FeatureKey == "enabled").BooleanValue);
        Assert.Equal("pro", updated.Entitlements.Single(value => value.FeatureKey == "tier").StringValue);

        values["regions"] = EntitlementValue.StringSet(new[] { "apac", "eu", "us" });
        updated = await InTransactionAsync(() => Catalog.UpdatePlanAsync(
            updated.Id, updated.ConcurrencyStamp, new CatalogDetails("Allowed set addition"), values));
        Assert.Equal(
            new[] { "apac", "eu", "us" },
            updated.Entitlements.Single(value => value.FeatureKey == "regions")
                .ToValue()
                .StringValues);

        values["regions"] = EntitlementValue.StringSet(new[] { "apac", "eu", "missing", "us" });
        var rejectedSet = await Assert.ThrowsAsync<BusinessException>(() => InTransactionAsync(() =>
            Catalog.UpdatePlanAsync(updated.Id, updated.ConcurrencyStamp,
                new CatalogDetails("Rejected set addition"), values)));
        Assert.Equal(SubscriptionErrorCodes.EntitlementOptionNotAllowed, rejectedSet.Code);
        values["regions"] = EntitlementValue.StringSet(new[] { "apac", "eu", "us" });

        values["tier"] = EntitlementValue.Enum("enterprise");
        var rejected = await Assert.ThrowsAsync<BusinessException>(() => InTransactionAsync(() =>
            Catalog.UpdatePlanAsync(updated.Id, updated.ConcurrencyStamp,
                new CatalogDetails("Changed option"), values)));
        Assert.Equal(SubscriptionErrorCodes.EntitlementOptionNotAllowed, rejected.Code);

        var withdrawn = await InTransactionAsync(() => Catalog.SetPlanStateAsync(
            updated.Id, updated.ConcurrencyStamp, SubscriptionCatalogState.Withdrawn));
        var publish = await Assert.ThrowsAsync<BusinessException>(() => InTransactionAsync(() =>
            Catalog.SetPlanStateAsync(withdrawn.Id, withdrawn.ConcurrencyStamp,
                SubscriptionCatalogState.Published)));
        Assert.Equal(SubscriptionErrorCodes.EntitlementOptionNotAllowed, publish.Code);
    }

    [Fact]
    public async Task Entitlement_model_maps_bounded_text_storage_and_all_four_type_shapes()
    {
        await InTransactionAsync(async () =>
        {
            var db = await GetRequiredService<IDbContextProvider<ISubscriptionDbContext>>().GetDbContextAsync();
            var model = ((DbContext)db).GetService<IDesignTimeModel>().Model;
            foreach (var entityType in new[]
                     {
                         model.FindEntityType(typeof(SubscriptionPlanEntitlement))!,
                         model.FindEntityType(typeof(UserSubscriptionEntitlement))!
                     })
            {
                Assert.Equal(SubscriptionConsts.MaxEntitlementStringLength,
                    entityType.FindProperty(nameof(SubscriptionPlanEntitlement.StringValue))!.GetMaxLength());
                Assert.Equal(SubscriptionConsts.MaxEntitlementStringSetStorageLength,
                    entityType.FindProperty(nameof(SubscriptionPlanEntitlement.StringSetValue))!.GetMaxLength());
                var constraint = Assert.Single(entityType.GetCheckConstraints(),
                    check => check.Name?.Contains("Value", StringComparison.Ordinal) == true);
                Assert.Contains("\"ValueType\" = 0", constraint.Sql!);
                Assert.Contains("\"ValueType\" = 1", constraint.Sql!);
                Assert.Contains("\"ValueType\" = 2", constraint.Sql!);
                Assert.Contains("\"ValueType\" = 3", constraint.Sql!);
            }

            return true;
        });
    }

    private async Task<bool> DeleteProductAsync(Guid id, string stamp)
    {
        await Catalog.DeleteProductAsync(id, stamp);
        return true;
    }
}
