using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SayHello.Subscription;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

public class SubscriptionHostDefaultPlanTests : WebHostEntityFrameworkCoreTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Default_Pointer_Should_Roundtrip_Replace_And_Clear_Without_Creating_Subscriptions(bool hasTenant)
    {
        using var tenant = GetRequiredService<ICurrentTenant>().Change(hasTenant ? Guid.NewGuid() : null);
        var data = await SeedAsync();
        await WithUnitOfWorkAsync(async () =>
        {
            var context = await ContextAsync();
            var product = await context.Set<SubscriptionProduct>().SingleAsync(value => value.Id == data.ProductId);
            product.DefaultPlanId.ShouldBeNull();
            product.SetDefaultPlan(await context.Set<SubscriptionPlan>().SingleAsync(value => value.Id == data.PlanId));
        });
        await WithUnitOfWorkAsync(async () =>
        {
            var context = await ContextAsync();
            var product = await context.Set<SubscriptionProduct>().SingleAsync(value => value.Id == data.ProductId);
            product.DefaultPlanId.ShouldBe(data.PlanId);
            product.SetDefaultPlan(await context.Set<SubscriptionPlan>().SingleAsync(value => value.Id == data.ReplacementId));
        });
        await WithUnitOfWorkAsync(async () =>
        {
            var context = await ContextAsync();
            var product = await context.Set<SubscriptionProduct>().SingleAsync(value => value.Id == data.ProductId);
            product.DefaultPlanId.ShouldBe(data.ReplacementId);
            product.SetDefaultPlan(null);
        });
        await WithUnitOfWorkAsync(async () =>
        {
            var context = await ContextAsync();
            (await context.Set<SubscriptionProduct>().SingleAsync(value => value.Id == data.ProductId))
                .DefaultPlanId.ShouldBeNull();
            (await context.Set<UserSubscription>().CountAsync(value => value.ProductId == data.ProductId)).ShouldBe(0);
            (await context.Set<SubscriptionPlan>().CountAsync(value => value.ProductId == data.ProductId)).ShouldBe(2);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_Should_Reject_Cross_Product_Pointers_And_Deletion_Until_Cleared(bool hasTenant)
    {
        using var tenant = GetRequiredService<ICurrentTenant>().Change(hasTenant ? Guid.NewGuid() : null);
        var data = await SeedAsync();
        await WithUnitOfWorkAsync(async () =>
        {
            var context = await ContextAsync();
            var invalid = await Should.ThrowAsync<SqliteException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "SubscriptionProducts" SET "DefaultPlanId" = {data.PlanId} WHERE "Id" = {data.OtherProductId}"""));
            invalid.SqliteExtendedErrorCode.ShouldBe(787);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "SubscriptionProducts" SET "DefaultPlanId" = {data.PlanId} WHERE "Id" = {data.ProductId}""");
            var referenced = await Should.ThrowAsync<SqliteException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM "SubscriptionPlans" WHERE "Id" = {data.PlanId}"""));
            referenced.SqliteErrorCode.ShouldBe(19);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "SubscriptionProducts" SET "DefaultPlanId" = NULL WHERE "Id" = {data.ProductId}""");
            (await context.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM "SubscriptionPlans" WHERE "Id" = {data.PlanId}""")).ShouldBe(1);
        });
        await WithUnitOfWorkAsync(async () =>
        {
            var context = await ContextAsync();
            (await context.Set<SubscriptionProduct>().SingleAsync(value => value.Id == data.ProductId))
                .DefaultPlanId.ShouldBeNull();
            (await context.Set<SubscriptionProduct>().SingleAsync(value => value.Id == data.OtherProductId))
                .DefaultPlanId.ShouldBeNull();
            (await context.Set<SubscriptionPlan>().AnyAsync(value => value.Id == data.PlanId)).ShouldBeFalse();
        });
    }

    private Task<CatalogData> SeedAsync() => WithUnitOfWorkAsync(async () =>
    {
        var context = await ContextAsync();
        var definition = new ProductDefinition("host-default-test", new FixedLocalizableString("Host default test"),
            [new FeatureDefinition("limit", new FixedLocalizableString("Limit"), SubscriptionEntitlementType.Numeric)]);
        var product = new SubscriptionProduct(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, definition, "Product");
        var other = new SubscriptionProduct(Guid.NewGuid(), product.TenantId,
            new ProductDefinition("host-other-test", new FixedLocalizableString("Other"), []), "Other");
        product.Publish();
        other.Publish();
        context.AddRange(product, other);
        await context.SaveChangesAsync();
        var plan = new SubscriptionPlan(Guid.NewGuid(), product, "free", "Free");
        plan.ReplaceEntitlements(definition, new Dictionary<string, EntitlementValue> { ["limit"] = EntitlementValue.Numeric(20) });
        plan.Publish(product, definition);
        var replacement = new SubscriptionPlan(Guid.NewGuid(), product, "replacement", "Replacement");
        replacement.ReplaceEntitlements(definition,
            new Dictionary<string, EntitlementValue> { ["limit"] = EntitlementValue.Numeric(30) });
        replacement.Publish(product, definition);
        context.AddRange(plan, replacement);
        return new CatalogData(product.Id, plan.Id, replacement.Id, other.Id);
    });

    private Task<WebHostDbContext> ContextAsync() =>
        GetRequiredService<IDbContextProvider<WebHostDbContext>>().GetDbContextAsync();

    private sealed record CatalogData(Guid ProductId, Guid PlanId, Guid ReplacementId, Guid OtherProductId);
}
