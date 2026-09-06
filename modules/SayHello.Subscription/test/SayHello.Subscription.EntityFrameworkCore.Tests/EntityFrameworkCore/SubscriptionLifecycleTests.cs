using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.Subscription.EntityFrameworkCore;

public class SubscriptionLifecycleTests : SubscriptionPersistenceTestBase
{
    [Fact]
    public async Task Replacing_A_preserves_B_identity_snapshot_and_expiration()
    {
        var data = await SeedAsync();
        var first = await AssignBundleAsync(data, data.AB,
            item => TestClock.Now.AddDays(item.ProductCode == "alpha" ? 1 : 10));
        var a = first.Single(x => x.ProductCode == "alpha");
        var b = first.Single(x => x.ProductCode == "beta");
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(a.AssignmentId, b.AssignmentId);
        await InTransactionAsync(() => Catalog.UpdatePlanAsync(null, data.Plans[0].Id, data.Plans[0].ConcurrencyStamp,
            new CatalogDetails("Changed"), SubscriptionTestDefinitions.Values(99)));
        await InTransactionAsync(async () =>
        {
            var checker = GetRequiredService<ISubscriptionEntitlementChecker>();
            Assert.Equal(10, (await checker.GetNumericAsync(null, data.UserId, "alpha", "limit")).Limit);
            return true;
        });

        var replacement = await AssignPlanAsync(data, 0, TestClock.Now.AddDays(2));
        Assert.NotEqual(a.Id, replacement.Id);
        await InTransactionAsync(async () =>
        {
            var current = await Subscriptions.GetCurrentListAsync(null, data.UserId);
            var retained = current.Single(x => x.ProductCode == "beta");
            Assert.Equal(b.Id, retained.Id);
            Assert.Equal(b.ConcurrencyStamp, retained.ConcurrencyStamp);
            Assert.Equal(b.ExpiresAt, retained.ExpiresAt);
            Assert.Equal(10, retained.Entitlements.Single(x => x.FeatureKey == "limit").NumericValue);
            Assert.Equal(99, current.Single(x => x.ProductCode == "alpha").Entitlements.Single(x => x.FeatureKey == "limit").NumericValue);
            var history = await Subscriptions.GetAsync(null, a.Id);
            Assert.False(history.IsCurrent);
            Assert.Equal(SubscriptionEndReason.Replaced, history.EndReason);
            Assert.Equal(10, history.Entitlements.Single(x => x.FeatureKey == "limit").NumericValue);
            Assert.Equal(3, (await Subscriptions.GetPageAsync(new UserSubscriptionQuery(null, TestClock.Now, data.UserId))).TotalCount);
            return true;
        });
    }

    [Fact]
    public async Task Assigning_AC_to_AB_replaces_only_A_and_expirations_are_independent()
    {
        var data = await SeedAsync();
        var first = await AssignBundleAsync(data, data.AB);
        var b = first.Single(x => x.ProductCode == "beta");
        var next = await AssignBundleAsync(data, data.AC, item =>
            item.ProductCode == "alpha" ? TestClock.Now.AddHours(1) : TestClock.Now.AddDays(2));
        Assert.Equal(2, next.Count);
        TestClock.Now = TestClock.Now.AddHours(1);
        await InTransactionAsync(async () =>
        {
            var checker = GetRequiredService<ISubscriptionEntitlementChecker>();
            Assert.Null(await checker.FindEffectiveSubscriptionAsync(null, data.UserId, "alpha"));
            Assert.NotNull(await checker.FindEffectiveSubscriptionAsync(null, data.UserId, "gamma"));
            Assert.Equal(b.Id, (await checker.FindEffectiveSubscriptionAsync(null, data.UserId, "beta"))!.Id);
            Assert.Equal(3, (await Subscriptions.GetCurrentListAsync(null, data.UserId)).Count);
            var expired = await Subscriptions.GetPageAsync(new UserSubscriptionQuery(null, TestClock.Now,
                data.UserId, Status: UserSubscriptionStatus.Expired));
            Assert.Single(expired.Items);
            Assert.Equal("alpha", expired.Items[0].ProductCode);
            return true;
        });
        var renewed = await AssignPlanAsync(data, 0);
        Assert.NotEqual(next.Single(x => x.ProductCode == "alpha").Id, renewed.Id);
    }

    [Fact]
    public async Task Entitlement_queries_distinguish_no_subscription_missing_zero_and_unlimited()
    {
        var data = await SeedAsync();
        var checker = GetRequiredService<ISubscriptionEntitlementChecker>();
        await InTransactionAsync(async () =>
        {
            Assert.Equal(EntitlementGrantStatus.NoSubscription, (await checker.GetNumericAsync(null, data.UserId, "alpha", "limit")).Status);
            Assert.Equal(SubscriptionErrorCodes.UnknownFeature,
                (await Assert.ThrowsAsync<BusinessException>(() => checker.GetBooleanAsync(null, data.UserId, "alpha", "unknown"))).Code);
            return true;
        });
        var values = SubscriptionTestDefinitions.Values(0);
        var updated = await InTransactionAsync(() => Catalog.UpdatePlanAsync(null, data.Plans[0].Id,
            data.Plans[0].ConcurrencyStamp, new CatalogDetails("Zero"), values));
        await AssignPlanAsync(data, 0);
        await InTransactionAsync(async () =>
        {
            Assert.Equal(0, (await checker.RequireNumericAsync(null, data.UserId, "alpha", "limit", 0)).Limit);
            Assert.Equal(EntitlementGrantStatus.NotGranted, (await checker.GetNumericAsync(null, data.UserId, "alpha", "missing-limit")).Status);
            Assert.False((await checker.GetBooleanAsync(null, data.UserId, "alpha", "future")).IsGranted);
            Assert.Equal(SubscriptionErrorCodes.EntitlementNotGranted,
                (await Assert.ThrowsAsync<BusinessException>(() => checker.RequireBooleanAsync(null, data.UserId, "alpha", "future"))).Code);
            Assert.Equal(SubscriptionErrorCodes.EntitlementTypeMismatch,
                (await Assert.ThrowsAsync<BusinessException>(() => checker.GetBooleanAsync(null, data.UserId, "alpha", "limit"))).Code);
            return true;
        });
        values["limit"] = EntitlementValue.Unlimited();
        await InTransactionAsync(() => Catalog.UpdatePlanAsync(null, updated.Id, updated.ConcurrencyStamp, new CatalogDetails("Unlimited"), values));
        await AssignPlanAsync(data, 0);
        await InTransactionAsync(async () =>
        {
            var unlimited = await checker.RequireNumericAsync(null, data.UserId, "alpha", "limit", long.MaxValue);
            Assert.True(unlimited.IsUnlimited);
            Assert.Null(unlimited.Limit);
            return true;
        });
    }

    [Fact]
    public async Task Expiration_adjustment_and_revoke_preserve_snapshots_and_detect_stale_versions()
    {
        var data = await SeedAsync();
        var assignment = await AssignPlanAsync(data, 0, TestClock.Now.AddDays(1));
        var oldStamp = assignment.ConcurrencyStamp;
        var permanent = await InTransactionAsync(() => Manager.AdjustExpirationAsync(null, assignment.Id, oldStamp, null));
        Assert.Null(permanent.ExpiresAt);
        Assert.NotEqual(oldStamp, permanent.ConcurrencyStamp);
        var stale = await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => Manager.RevokeAsync(null, assignment.Id, oldStamp, "stale")));
        Assert.Equal(SubscriptionErrorCodes.ConcurrencyConflict, stale.Code);
        var revoked = await InTransactionAsync(() => Manager.RevokeAsync(null, assignment.Id, permanent.ConcurrencyStamp, "requested"));
        Assert.Equal(SubscriptionEndReason.Revoked, revoked.EndReason);
        Assert.False(revoked.IsCurrent);
        Assert.Equal(assignment.Entitlements.Count, revoked.Entitlements.Count);
        Assert.Equal(SubscriptionErrorCodes.NoEffectiveSubscription,
            (await Assert.ThrowsAsync<BusinessException>(() => InTransactionAsync(() =>
                Manager.AdjustExpirationAsync(null, revoked.Id, revoked.ConcurrencyStamp, TestClock.Now.AddDays(5))))).Code);
    }

    [Fact]
    public async Task Stale_current_and_catalog_preview_versions_are_rejected_without_replacement()
    {
        var data = await SeedAsync();
        var initialPreview = await InTransactionAsync(() => Manager.PreviewPlanAsync(null, data.UserId, data.Plans[0].Id));
        var assigned = await AssignPlanAsync(data, 0);
        var stale = new AssignSubscriptionPlan(null, data.UserId, Target(initialPreview.Items[0]));
        Assert.Equal(SubscriptionErrorCodes.ConcurrencyConflict, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => Manager.AssignPlanAsync(stale)))).Code);
        var preview = await InTransactionAsync(() => Manager.PreviewPlanAsync(null, data.UserId, data.Plans[0].Id));
        await InTransactionAsync(() => Catalog.UpdatePlanAsync(null, data.Plans[0].Id, data.Plans[0].ConcurrencyStamp,
            new CatalogDetails("New revision"), SubscriptionTestDefinitions.Values(12)));
        Assert.Equal(SubscriptionErrorCodes.ConcurrencyConflict, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => Manager.AssignPlanAsync(new AssignSubscriptionPlan(null, data.UserId, Target(preview.Items[0])))))).Code);
        await InTransactionAsync(async () =>
        {
            Assert.Equal(assigned.Id, (await Subscriptions.FindCurrentAsync(null, data.UserId, data.Products[0].Id))!.Id);
            return true;
        });
    }

    [Fact]
    public async Task Mutation_lock_remains_held_after_method_return_until_transaction_completion()
    {
        var data = await SeedAsync();
        var locks = GetRequiredService<SubscriptionTestDistributedLock>();
        using (var unit = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var preview = await Manager.PreviewPlanAsync(null, data.UserId, data.Plans[0].Id);
            await Manager.AssignPlanAsync(new AssignSubscriptionPlan(null, data.UserId, Target(preview.Items[0])));
            var context = await GetRequiredService<IDbContextProvider<ISubscriptionDbContext>>().GetDbContextAsync();
            Assert.True(unit.Options.IsTransactional);
            Assert.NotNull(context.Database.CurrentTransaction);
            Assert.Equal(1, locks.HeldCount);
            await unit.CompleteAsync();
            Assert.Equal(0, locks.HeldCount);
        }
    }

    [Fact]
    public async Task Missing_user_and_tenant_mismatch_fail_closed()
    {
        var data = await SeedAsync();
        Assert.Equal(SubscriptionErrorCodes.UserNotFound, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => Manager.PreviewPlanAsync(null, Guid.NewGuid(), data.Plans[0].Id)))).Code);
        Assert.Equal(SubscriptionErrorCodes.TenantMismatch, (await Assert.ThrowsAsync<BusinessException>(() =>
            InTransactionAsync(() => Manager.PreviewPlanAsync(Guid.NewGuid(), data.UserId, data.Plans[0].Id)))).Code);
    }
}
