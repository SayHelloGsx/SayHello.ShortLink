using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Localization;
using Xunit;

namespace SayHello.Subscription;

public class SubscriptionAggregateTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Definitions_normalize_codes_and_reject_duplicates_types_and_invalid_limits()
    {
        Assert.Equal("alpha", SubscriptionTestDefinitions.Product(" ALPHA ").Code);
        var numeric = SubscriptionTestDefinitions.Product("alpha").GetFeature("LIMIT");
        numeric.Validate(EntitlementValue.Numeric(0));
        numeric.Validate(EntitlementValue.Unlimited());
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () => EntitlementValue.Numeric(-1));
        AssertCode(SubscriptionErrorCodes.EntitlementTypeMismatch, () => numeric.Validate(EntitlementValue.Boolean(true)));
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () =>
            SubscriptionTestDefinitions.Product("alpha").GetFeature("capped").Validate(EntitlementValue.Unlimited()));
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () =>
            SubscriptionTestDefinitions.Product("alpha").GetFeature("capped").Validate(EntitlementValue.Numeric(101)));
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () =>
            EntitlementValue.FromStorage(SubscriptionEntitlementType.Numeric, true, null, true));
        Assert.Throws<AbpException>(() => new ProductDefinition("alpha", new FixedLocalizableString("A"), new[]
        {
            new FeatureDefinition("Feature", new FixedLocalizableString("F"), SubscriptionEntitlementType.Boolean),
            new FeatureDefinition("FEATURE", new FixedLocalizableString("F"), SubscriptionEntitlementType.Boolean)
        }));
        AssertCode(SubscriptionErrorCodes.InvalidCode, () => SubscriptionCode.Normalize("../alpha"));
    }

    [Fact]
    public void Registry_builds_once_and_rejects_unknown_and_duplicate_products()
    {
        using var services = new ServiceCollection().AddTransient<SubscriptionTestDefinitions>()
            .AddTransient<DuplicateDefinitions>().BuildServiceProvider();
        var options = new SubscriptionDefinitionOptions();
        options.DefinitionProviders.Add<SubscriptionTestDefinitions>();
        var registry = new SubscriptionDefinitionRegistry(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options));
        Assert.Equal(3, registry.GetProducts().Count);
        Assert.Same(registry.GetProduct("alpha"), registry.GetProduct("ALPHA"));
        AssertCode(SubscriptionErrorCodes.UnknownProduct, () => registry.GetProduct("unknown"));
        AssertCode(SubscriptionErrorCodes.UnknownFeature, () => registry.GetFeature("alpha", "unknown"));
        var duplicates = new SubscriptionDefinitionOptions();
        duplicates.DefinitionProviders.Add<SubscriptionTestDefinitions>();
        duplicates.DefinitionProviders.Add<DuplicateDefinitions>();
        var invalid = new SubscriptionDefinitionRegistry(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(duplicates));
        Assert.Throws<AbpException>(() => invalid.GetProducts());
    }

    [Fact]
    public void Snapshots_remain_immutable_when_catalog_values_and_labels_change()
    {
        var (product, plan) = Catalog();
        var subscription = Assign(product, plan, Now.AddHours(1));
        product.UpdateDetails("Renamed product", null, 0);
        plan.UpdateDetails("Renamed plan", null, 0);
        plan.ReplaceEntitlements(SubscriptionTestDefinitions.Product("alpha"), SubscriptionTestDefinitions.Values(999));
        Assert.Equal("alpha", subscription.ProductName);
        Assert.Equal("basic", subscription.PlanName);
        Assert.Equal(10, subscription.Entitlements.Single(x => x.FeatureKey == "limit").NumericValue);
        Assert.All(subscription.Entitlements, row =>
            Assert.All(row.GetType().GetProperties().Where(p => p.SetMethod != null), p => Assert.False(p.SetMethod!.IsPublic)));
        Assert.Throws<NotSupportedException>(() => ((ICollection<UserSubscriptionEntitlement>)subscription.Entitlements).Clear());
    }

    [Fact]
    public void Expiration_is_exclusive_and_ended_or_expired_rows_cannot_be_reactivated()
    {
        var (product, plan) = Catalog();
        var subscription = Assign(product, plan, Now.AddHours(1));
        Assert.True(subscription.IsEffectiveAt(Now));
        Assert.False(subscription.IsEffectiveAt(Now.AddHours(1)));
        Assert.Equal(UserSubscriptionStatus.Expired, subscription.GetStatus(Now.AddHours(1)));
        AssertCode(SubscriptionErrorCodes.NoEffectiveSubscription, () => subscription.AdjustExpiration(Now.AddHours(1), null));
        subscription.End(Now.AddHours(1), SubscriptionEndReason.Replaced);
        Assert.False(subscription.IsCurrent);
        Assert.Equal(Now.AddHours(1), subscription.EndedAt);
        AssertCode(SubscriptionErrorCodes.InvalidState, () => subscription.End(Now.AddHours(2), SubscriptionEndReason.Revoked));
        var permanent = Assign(product, plan, null);
        Assert.True(permanent.IsEffectiveAt(Now.AddYears(20)));
        AssertCode(SubscriptionErrorCodes.InvalidExpiration, () => Assign(product, plan, Now));
    }

    [Fact]
    public void Bundle_requires_distinct_products_and_republication_after_component_changes()
    {
        var (product, plan) = Catalog();
        var alternate = new SubscriptionPlan(Guid.NewGuid(), product, "other", "Other");
        alternate.Publish(product, SubscriptionTestDefinitions.Product("alpha"));
        AssertCode(SubscriptionErrorCodes.InvalidBundle, () =>
            new SubscriptionBundle(Guid.NewGuid(), null, "bundle", "Bundle", new[] { plan, alternate }));
        var (beta, betaPlan) = Catalog("beta");
        var bundle = new SubscriptionBundle(Guid.NewGuid(), null, "bundle", "Bundle", new[] { plan, betaPlan });
        bundle.Publish(new[] { plan, betaPlan }, new[] { product, beta });
        bundle.ReplaceItems(new[] { plan, betaPlan });
        Assert.Equal(SubscriptionCatalogState.Published, bundle.State);
        bundle.ReplaceItems(new[] { alternate, betaPlan });
        Assert.Equal(SubscriptionCatalogState.Draft, bundle.State);
        beta.Withdraw();
        AssertCode(SubscriptionErrorCodes.CatalogUnavailable, () => bundle.Publish(new[] { alternate, betaPlan }, new[] { product, beta }));
    }

    [Fact]
    public void Numeric_results_distinguish_absent_zero_and_unlimited()
    {
        var id = Guid.NewGuid();
        Assert.False(NumericEntitlementResult.NoSubscription().Allows(0));
        Assert.False(NumericEntitlementResult.NotGranted(id).Allows(0));
        Assert.True(NumericEntitlementResult.Finite(id, 0).Allows(0));
        Assert.False(NumericEntitlementResult.Finite(id, 0).Allows(1));
        Assert.True(NumericEntitlementResult.Unlimited(id).Allows(long.MaxValue));
        Assert.Null(NumericEntitlementResult.Unlimited(id).Limit);
    }

    private static (SubscriptionProduct, SubscriptionPlan) Catalog(string code = "alpha")
    {
        var definition = SubscriptionTestDefinitions.Product(code);
        var product = new SubscriptionProduct(Guid.NewGuid(), null, definition, code);
        product.Publish();
        var plan = new SubscriptionPlan(Guid.NewGuid(), product, "basic", "basic");
        plan.ReplaceEntitlements(definition, SubscriptionTestDefinitions.Values());
        plan.Publish(product, definition);
        return (product, plan);
    }

    private static UserSubscription Assign(SubscriptionProduct product, SubscriptionPlan plan, DateTime? expiresAt) =>
        new(Guid.NewGuid(), Guid.NewGuid(), product, plan, plan.Entitlements.Select(e =>
            new EntitlementSnapshotData(e.FeatureKey, e.FeatureKey, e.ToValue())).ToArray(), Now, expiresAt, Guid.NewGuid());

    private static void AssertCode(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<BusinessException>(action).Code);

    public class DuplicateDefinitions : SubscriptionDefinitionProvider
    {
        public override void Define(ISubscriptionDefinitionContext context) => context.AddProduct(SubscriptionTestDefinitions.Product("ALPHA"));
    }
}
