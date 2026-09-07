using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.Subscription.EntityFrameworkCore;

public class SubscriptionDefaultPlanTests : SubscriptionPersistenceTestBase
{
    private ISubscriptionEntitlementChecker Checker => GetRequiredService<ISubscriptionEntitlementChecker>();

    [Fact]
    public async Task Free_is_resolved_without_an_assignment_or_ambient_unit_and_compares_20_and_21()
    {
        var data = await SeedAsync();
        var free = await ConfigureFreeAsync(data);
        var result = await Checker.RequireNumericAsync(null, data.UserId, "ALPHA", "LIMIT", 20);
        Assert.Equal(20, result.Limit);
        Assert.Equal(EntitlementSource.DefaultPlan, result.Source);
        Assert.Equal(free.Id, result.PlanId);
        Assert.Null(result.SubscriptionId);
        Assert.Equal(SubscriptionErrorCodes.EntitlementNotGranted,
            (await Assert.ThrowsAsync<BusinessException>(() => Checker.RequireNumericAsync(null, data.UserId, "alpha", "limit", 21))).Code);
        Assert.True((await Checker.GetBooleanAsync(null, data.UserId, "alpha", "enabled")).IsGranted);
        var context = await Checker.ResolveAsync(null, data.UserId, "alpha");
        Assert.Equal(EntitlementSource.DefaultPlan, context.Source);
        Assert.Equal(free.Id, context.DefaultPlan!.Plan.Id);
        Assert.Null(context.Subscription);
        Assert.Null(await Checker.FindEffectiveSubscriptionAsync(null, data.UserId, "alpha"));
        Assert.Equal(0, (await Subscriptions.GetPageAsync(new UserSubscriptionQuery(null, TestClock.Now, data.UserId))).TotalCount);
        Assert.Null(GetRequiredService<IUnitOfWorkManager>().Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pro_snapshot_takes_precedence_and_expiration_or_revocation_restores_live_Free(bool revoke)
    {
        var data = await SeedAsync();
        var free = await ConfigureFreeAsync(data);
        var subscription = await AssignProAsync(data, TestClock.Now.AddHours(1));
        var pro = await Checker.RequireNumericAsync(null, data.UserId, "alpha", "limit", 100);
        Assert.Equal(100, pro.Limit);
        Assert.Equal(subscription.Id, pro.SubscriptionId);
        Assert.Equal(subscription.SourcePlanId, pro.PlanId);
        Assert.Equal(EntitlementSource.Subscription, pro.Source);
        Assert.Equal(SubscriptionErrorCodes.EntitlementNotGranted,
            (await Assert.ThrowsAsync<BusinessException>(() => Checker.RequireNumericAsync(null, data.UserId, "alpha", "limit", 101))).Code);

        await InTransactionAsync(async () =>
        {
            await Catalog.UpdatePlanAsync(null, free.Id, free.ConcurrencyStamp, new CatalogDetails("Free updated"),
                SubscriptionTestDefinitions.Values(25));
            var plan = await GetRequiredService<ISubscriptionPlanRepository>().GetAsync(subscription.SourcePlanId);
            await Catalog.UpdatePlanAsync(null, plan.Id, plan.ConcurrencyStamp, new CatalogDetails("Pro updated"),
                SubscriptionTestDefinitions.Values(200));
            return true;
        });
        Assert.Equal(100, (await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit")).Limit);
        if (revoke)
            await Manager.RevokeAsync(null, subscription.Id, subscription.ConcurrencyStamp, "test");
        else
            TestClock.Now = subscription.ExpiresAt!.Value;

        var fallback = await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit");
        Assert.Equal(25, fallback.Limit);
        Assert.Equal(EntitlementSource.DefaultPlan, fallback.Source);
        Assert.Null(fallback.SubscriptionId);
        Assert.Equal(1, (await Subscriptions.GetPageAsync(new UserSubscriptionQuery(null, TestClock.Now, data.UserId))).TotalCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Active_snapshot_does_not_inherit_missing_false_or_zero_values_from_Free(bool omitLimit)
    {
        var data = await SeedAsync();
        await ConfigureFreeAsync(data);
        var subscription = await InTransactionAsync(async () =>
        {
            var values = SubscriptionTestDefinitions.Values(0);
            values["enabled"] = EntitlementValue.Boolean(false);
            if (omitLimit) values.Remove("limit");
            var plan = await Catalog.CreatePlanAsync(null, data.Products[0].Id, "restricted", new CatalogDetails("Restricted"), values);
            plan = await Catalog.SetPlanStateAsync(null, plan.Id, plan.ConcurrencyStamp, SubscriptionCatalogState.Published);
            var preview = await Manager.PreviewPlanAsync(null, data.UserId, plan.Id);
            return await Manager.AssignPlanAsync(new AssignSubscriptionPlan(null, data.UserId, Target(preview.Items[0])));
        });
        var numeric = await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit");
        Assert.Equal(subscription.Id, numeric.SubscriptionId);
        Assert.Equal(EntitlementSource.Subscription, numeric.Source);
        Assert.Equal(omitLimit ? EntitlementGrantStatus.NotGranted : EntitlementGrantStatus.Granted, numeric.Status);
        Assert.Equal(omitLimit ? (long?)null : 0, numeric.Limit);
        Assert.False((await Checker.GetBooleanAsync(null, data.UserId, "alpha", "enabled")).IsGranted);
        Assert.False((await Checker.GetBooleanAsync(null, data.UserId, "alpha", "future")).IsGranted);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("missing")]
    [InlineData("unlimited")]
    public async Task Default_values_preserve_missing_zero_unlimited_and_type_validation(string kind)
    {
        var data = await SeedAsync();
        var free = await ConfigureFreeAsync(data);
        var values = SubscriptionTestDefinitions.Values(0);
        values["enabled"] = EntitlementValue.Boolean(false);
        if (kind == "missing") values.Remove("limit");
        if (kind == "unlimited") values["limit"] = EntitlementValue.Unlimited();
        await Catalog.UpdatePlanAsync(null, free.Id, free.ConcurrencyStamp, new CatalogDetails("Free"), values);
        var result = await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit");
        Assert.Equal(EntitlementSource.DefaultPlan, result.Source);
        Assert.Equal(kind != "missing", result.Allows(0));
        Assert.Equal(kind == "unlimited", result.Allows(long.MaxValue));
        Assert.Equal(kind == "zero" ? 0 : (long?)null, result.Limit);
        Assert.Equal(kind == "unlimited", result.IsUnlimited);
        Assert.Equal(EntitlementGrantStatus.NotGranted, (await Checker.GetBooleanAsync(null, data.UserId, "alpha", "enabled")).Status);
        Assert.Equal(SubscriptionErrorCodes.UnknownFeature,
            (await Assert.ThrowsAsync<BusinessException>(() => Checker.GetNumericAsync(null, data.UserId, "alpha", "unknown"))).Code);
        Assert.Equal(SubscriptionErrorCodes.EntitlementTypeMismatch,
            (await Assert.ThrowsAsync<BusinessException>(() => Checker.GetBooleanAsync(null, data.UserId, "alpha", "limit"))).Code);
    }

    [Fact]
    public async Task Default_listing_excludes_effective_subscribers_before_paging_and_preserves_history()
    {
        var data = await SeedAsync();
        for (var index = 0; index < 3; index++) await ConfigureFreeAsync(data, index);
        await AssignPlanAsync(data, 0);
        var query = new SubscriptionCatalogQuery(null, Sorting: SubscriptionCatalogSort.Name, MaxResultCount: 1);
        var first = await Checker.GetDefaultPlansAsync(null, data.UserId, query);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal("beta", Assert.Single(first.Items).Product.Code);
        var second = await Checker.GetDefaultPlansAsync(null, data.UserId, query with { SkipCount = 1 });
        Assert.Equal(2, second.TotalCount);
        Assert.Equal("gamma", Assert.Single(second.Items).Product.Code);
        var filtered = await Checker.GetDefaultPlansAsync(null, data.UserId, query with { ProductId = data.Products[0].Id });
        Assert.Empty(filtered.Items);
        var searched = await Checker.GetDefaultPlansAsync(null, data.UserId, query with { Filter = "GAMMA" });
        Assert.Equal(1, searched.TotalCount);
        Assert.Equal("gamma", Assert.Single(searched.Items).Product.Code);
        Assert.Equal(1, (await Subscriptions.GetPageAsync(new UserSubscriptionQuery(null, TestClock.Now, data.UserId))).TotalCount);
    }

    [Fact]
    public async Task Defaults_are_product_and_tenant_scoped_even_with_filters_disabled()
    {
        var host = await SeedAsync();
        var tenantId = Guid.NewGuid();
        var tenant = await SeedAsync(tenantId);
        await ConfigureFreeAsync(host);
        var tenantPlan = await ConfigureFreeAsync(tenant, 1);
        using (GetRequiredService<IDataFilter<IMultiTenant>>().Disable())
        {
            Assert.Equal(EntitlementSource.DefaultPlan, (await Checker.ResolveAsync(null, host.UserId, "alpha")).Source);
            Assert.Equal(EntitlementSource.None, (await Checker.ResolveAsync(null, host.UserId, "beta")).Source);
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                Assert.Equal(EntitlementSource.None, (await Checker.ResolveAsync(tenantId, tenant.UserId, "alpha")).Source);
                Assert.Equal(tenantPlan.Id, (await Checker.ResolveAsync(tenantId, tenant.UserId, "beta")).PlanId);
                var page = await Checker.GetDefaultPlansAsync(tenantId, tenant.UserId, new SubscriptionCatalogQuery(tenantId));
                Assert.Equal(tenantPlan.Id, Assert.Single(page.Items).Plan.Id);
                Assert.Equal(SubscriptionErrorCodes.TenantMismatch,
                    (await Assert.ThrowsAsync<BusinessException>(() => Checker.ResolveAsync(null, host.UserId, "alpha"))).Code);
            }
        }
        var product = await GetRequiredService<ISubscriptionProductRepository>().GetAsync(host.Products[0].Id);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, tenantPlan.Id));
    }

    [Fact]
    public async Task Default_changes_require_published_same_product_plans_and_fresh_versions()
    {
        var data = await SeedAsync();
        var product = data.Products[0];
        Assert.Equal(SubscriptionErrorCodes.InvalidDefaultPlan, (await Assert.ThrowsAsync<BusinessException>(() =>
            Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, data.Plans[1].Id))).Code);
        var draft = await Catalog.CreatePlanAsync(null, product.Id, "draft", new CatalogDetails("Draft"), SubscriptionTestDefinitions.Values());
        product = await GetRequiredService<ISubscriptionProductRepository>().GetAsync(product.Id);
        Assert.Equal(SubscriptionErrorCodes.InvalidDefaultPlan, (await Assert.ThrowsAsync<BusinessException>(() =>
            Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, draft.Id))).Code);
        var updated = await Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, data.Plans[0].Id);
        Assert.Equal(SubscriptionErrorCodes.ConcurrencyConflict, (await Assert.ThrowsAsync<BusinessException>(() =>
            Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, null))).Code);
        Assert.Equal(data.Plans[0].Id, (await GetRequiredService<ISubscriptionProductRepository>().GetAsync(updated.Id)).DefaultPlanId);
        await Catalog.SetDefaultPlanAsync(null, updated.Id, updated.ConcurrencyStamp, null);
        var result = await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit");
        Assert.Equal(EntitlementGrantStatus.NoSubscription, result.Status);
        Assert.Equal(EntitlementSource.None, result.Source);
        Assert.Equal(SubscriptionErrorCodes.NoEffectiveSubscription,
            (await Assert.ThrowsAsync<BusinessException>(() => Checker.RequireNumericAsync(null, data.UserId, "alpha", "limit", 0))).Code);
    }

    [Theory]
    [InlineData(SubscriptionCatalogState.Withdrawn)]
    [InlineData(SubscriptionCatalogState.Archived)]
    public async Task Default_lifecycle_is_guarded_until_it_is_explicitly_cleared(SubscriptionCatalogState state)
    {
        var data = await SeedAsync();
        var free = await ConfigureFreeAsync(data);
        var product = await GetRequiredService<ISubscriptionProductRepository>().GetAsync(data.Products[0].Id);
        Assert.Equal(SubscriptionErrorCodes.DefaultPlanInUse, (await Assert.ThrowsAsync<BusinessException>(() =>
            Catalog.SetProductStateAsync(null, product.Id, product.ConcurrencyStamp, state))).Code);
        Assert.Equal(SubscriptionErrorCodes.DefaultPlanInUse, (await Assert.ThrowsAsync<BusinessException>(() =>
            Catalog.SetPlanStateAsync(null, free.Id, free.ConcurrencyStamp, state))).Code);
        Assert.Equal(SubscriptionErrorCodes.DefaultPlanInUse, (await Assert.ThrowsAsync<BusinessException>(() =>
            Catalog.DeletePlanAsync(null, free.Id, free.ConcurrencyStamp))).Code);
        Assert.Equal(SubscriptionErrorCodes.DefaultPlanInUse, (await Assert.ThrowsAsync<BusinessException>(() =>
            Catalog.DeleteProductAsync(null, product.Id, product.ConcurrencyStamp))).Code);
        product = await Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, null);
        await Catalog.SetPlanStateAsync(null, free.Id, free.ConcurrencyStamp, state);
        product = await GetRequiredService<ISubscriptionProductRepository>().GetAsync(product.Id);
        await Catalog.SetProductStateAsync(null, product.Id, product.ConcurrencyStamp, state);
        Assert.Equal(EntitlementSource.None, (await Checker.ResolveAsync(null, data.UserId, "alpha")).Source);
        Assert.Equal(0, GetRequiredService<SubscriptionTestDistributedLock>().HeldCount);
    }

    [Fact]
    public async Task Default_pointer_cannot_reference_a_different_product_in_the_database()
    {
        var data = await SeedAsync();
        await using var connection = new SqliteConnection(GetRequiredService<SubscriptionTestDatabase>().ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """UPDATE "SubscriptionProducts" SET "DefaultPlanId" = $plan WHERE "Id" = $product;""";
        command.Parameters.AddWithValue("$plan", data.Plans[1].Id);
        command.Parameters.AddWithValue("$product", data.Products[0].Id);
        var exception = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(787, exception.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task Concurrent_default_selection_and_withdrawal_cannot_leave_an_unpublished_default()
    {
        var data = await SeedAsync();
        var product = data.Products[0];
        var plan = data.Plans[0];
        async Task<string> Attempt(bool select)
        {
            try
            {
                if (select) await Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, plan.Id);
                else await Catalog.SetPlanStateAsync(null, plan.Id, plan.ConcurrencyStamp, SubscriptionCatalogState.Withdrawn);
                return "success";
            }
            catch (BusinessException error)
            {
                return error.Code!;
            }
        }
        var outcomes = await Task.WhenAll(Task.Run(() => Attempt(true)), Task.Run(() => Attempt(false)));
        Assert.Single(outcomes, x => x == "success");
        Assert.Single(outcomes, x => x is SubscriptionErrorCodes.DefaultPlanInUse or
            SubscriptionErrorCodes.InvalidDefaultPlan or SubscriptionErrorCodes.ConcurrencyConflict);
        var persisted = await GetRequiredService<ISubscriptionProductRepository>().GetAsync(product.Id);
        var currentPlan = await GetRequiredService<ISubscriptionPlanRepository>().GetAsync(plan.Id);
        Assert.True(persisted.DefaultPlanId == null || currentPlan.State == SubscriptionCatalogState.Published);
        Assert.Equal(0, GetRequiredService<SubscriptionTestDistributedLock>().HeldCount);
    }

    [Fact]
    public async Task Default_lock_is_held_until_commit_and_released_after_rollback()
    {
        var data = await SeedAsync();
        var locks = GetRequiredService<SubscriptionTestDistributedLock>();
        using (var unit = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            await Catalog.SetDefaultPlanAsync(null, data.Products[0].Id, data.Products[0].ConcurrencyStamp, data.Plans[0].Id);
            Assert.Equal(1, locks.HeldCount);
            await unit.RollbackAsync();
            Assert.Equal(1, locks.HeldCount);
        }
        Assert.Equal(0, locks.HeldCount);
        Assert.Null((await GetRequiredService<ISubscriptionProductRepository>().GetAsync(data.Products[0].Id)).DefaultPlanId);
        await InTransactionAsync(async () =>
        {
            await Catalog.SetDefaultPlanAsync(null, data.Products[0].Id, data.Products[0].ConcurrencyStamp, data.Plans[0].Id);
            Assert.Equal(1, locks.HeldCount);
            return true;
        });
        Assert.Equal(0, locks.HeldCount);
    }

    [Fact]
    public async Task Creating_and_publishing_a_plan_holds_the_catalog_lock_before_saving()
    {
        var data = await SeedAsync();
        var locks = GetRequiredService<SubscriptionTestDistributedLock>();
        await InTransactionAsync(async () =>
        {
            var plan = await Catalog.CreatePlanAsync(null, data.Products[0].Id, "new-free",
                new CatalogDetails("New Free"), SubscriptionTestDefinitions.Values(30));
            Assert.Equal(1, locks.HeldCount);
            plan = await Catalog.SetPlanStateAsync(null, plan.Id, plan.ConcurrencyStamp, SubscriptionCatalogState.Published);
            var product = await GetRequiredService<ISubscriptionProductRepository>().GetAsync(plan.ProductId);
            await Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, plan.Id);
            Assert.Equal(1, locks.HeldCount);
            return true;
        });
        Assert.Equal(0, locks.HeldCount);
        Assert.Equal(30, (await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit")).Limit);
    }

    [Fact]
    public async Task Replacing_default_changes_live_rights_and_allows_withdrawing_the_old_plan()
    {
        var data = await SeedAsync();
        var old = await ConfigureFreeAsync(data);
        await InTransactionAsync(async () =>
        {
            var replacement = await Catalog.CreatePlanAsync(null, old.ProductId, "new-free",
                new CatalogDetails("New Free"), SubscriptionTestDefinitions.Values(30));
            await Catalog.SetPlanStateAsync(null, replacement.Id, replacement.ConcurrencyStamp, SubscriptionCatalogState.Published);
            var product = await GetRequiredService<ISubscriptionProductRepository>().GetAsync(old.ProductId);
            await Catalog.SetDefaultPlanAsync(null, product.Id, product.ConcurrencyStamp, replacement.Id);
            await Catalog.SetPlanStateAsync(null, old.Id, old.ConcurrencyStamp, SubscriptionCatalogState.Withdrawn);
            return true;
        });
        Assert.Equal(30, (await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit")).Limit);
        Assert.Equal("new-free", Assert.Single((await Checker.GetDefaultPlansAsync(null, data.UserId,
            new SubscriptionCatalogQuery(null))).Items).Plan.Code);
        Assert.Empty(await Subscriptions.GetCurrentListAsync(null, data.UserId));
    }

    [Fact]
    public async Task A_future_subscription_uses_Free_until_its_start_time()
    {
        var data = await SeedAsync();
        await ConfigureFreeAsync(data);
        var now = TestClock.Now;
        TestClock.Now = now.AddDays(1);
        var subscription = await AssignProAsync(data, TestClock.Now.AddHours(1));
        TestClock.Now = now;
        Assert.Equal(20, (await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit")).Limit);
        Assert.Single((await Checker.GetDefaultPlansAsync(null, data.UserId, new SubscriptionCatalogQuery(null))).Items);
        TestClock.Now = subscription.StartsAt;
        Assert.Equal(100, (await Checker.GetNumericAsync(null, data.UserId, "alpha", "limit")).Limit);
        Assert.Empty((await Checker.GetDefaultPlansAsync(null, data.UserId, new SubscriptionCatalogQuery(null))).Items);
    }

    [Fact]
    public async Task Default_products_plans_and_values_are_materialized_in_one_statement()
    {
        var data = await SeedAsync();
        await ConfigureFreeAsync(data);
        await InTransactionAsync(async () =>
        {
            var db = await GetRequiredService<IDbContextProvider<ISubscriptionDbContext>>().GetDbContextAsync();
            using var commands = new QueryCommandObserver(db);
            var repository = GetRequiredService<IDefaultSubscriptionPlanRepository>();
            var single = await repository.FindAsync(null, "alpha");
            Assert.NotNull(single);
            Assert.Equal(2, single.Plan.Entitlements.Count);
            Assert.Equal(1, commands.Count);
            var page = await repository.GetPageAsync(new SubscriptionCatalogQuery(null), data.UserId, TestClock.Now);
            Assert.Equal(2, Assert.Single(page.Items).Plan.Entitlements.Count);
            Assert.Equal(3, commands.Count); // Count plus one coherent product/plan/value query.
            return true;
        });
    }

    private sealed class QueryCommandObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly object _context;
        private readonly List<IDisposable> _subscriptions = new();
        public int Count { get; private set; }

        public QueryCommandObserver(object context)
        {
            _context = context;
            _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == DbLoggerCategory.Name)
                _subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value is CommandEventData command && ReferenceEquals(command.Context, _context)) Count++;
        }

        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
        public void Dispose()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
        }
    }

    private Task<SubscriptionPlan> ConfigureFreeAsync(CatalogData data, int index = 0) =>
        InTransactionAsync(async () =>
        {
            var plan = await GetRequiredService<ISubscriptionPlanRepository>().GetAsync(data.Plans[index].Id);
            plan = await Catalog.UpdatePlanAsync(data.TenantId, plan.Id, plan.ConcurrencyStamp,
                new CatalogDetails("Free " + plan.ProductCode), SubscriptionTestDefinitions.Values(20));
            var product = await GetRequiredService<ISubscriptionProductRepository>().GetAsync(plan.ProductId);
            await Catalog.SetDefaultPlanAsync(data.TenantId, product.Id, product.ConcurrencyStamp, plan.Id);
            return plan;
        }, data.TenantId);

    private Task<UserSubscription> AssignProAsync(CatalogData data, DateTime expiresAt) =>
        InTransactionAsync(async () =>
        {
            var plan = await Catalog.CreatePlanAsync(data.TenantId, data.Products[0].Id, "pro", new CatalogDetails("Pro"),
                SubscriptionTestDefinitions.Values(100));
            await Catalog.SetPlanStateAsync(data.TenantId, plan.Id, plan.ConcurrencyStamp, SubscriptionCatalogState.Published);
            var preview = await Manager.PreviewPlanAsync(data.TenantId, data.UserId, plan.Id);
            return await Manager.AssignPlanAsync(new AssignSubscriptionPlan(data.TenantId, data.UserId, Target(preview.Items[0], expiresAt)));
        }, data.TenantId);
}
