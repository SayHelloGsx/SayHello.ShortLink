using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public void String_entitlement_values_are_bounded_distinct_and_canonically_stored()
    {
        Assert.Equal(0, (int)SubscriptionEntitlementType.Boolean);
        Assert.Equal(1, (int)SubscriptionEntitlementType.Numeric);
        Assert.Equal(2, (int)SubscriptionEntitlementType.Enum);
        Assert.Equal(3, (int)SubscriptionEntitlementType.StringSet);

        var enumValue = EntitlementValue.Enum(" pro ");
        Assert.Equal("pro", enumValue.StringValue);
        var set = EntitlementValue.StringSet(new[] { "zeta", " alpha " });
        Assert.Equal(new[] { "alpha", "zeta" }, set.StringValues);
        Assert.Equal("""["alpha","zeta"]""", set.ToStorageStringSet());
        var restored = EntitlementValue.FromStorage(
            SubscriptionEntitlementType.StringSet, null, null, false, null, """["zeta","alpha"]""");
        Assert.True(set.HasSameValueAs(restored));
        Assert.Equal(set, restored);

        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () => EntitlementValue.Enum(""));
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () =>
            EntitlementValue.Enum(new string('x', SubscriptionConsts.MaxEntitlementStringLength + 1)));
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () =>
            EntitlementValue.StringSet(new[] { "same", "same" }));
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () =>
            EntitlementValue.StringSet(Enumerable.Range(0, SubscriptionConsts.MaxEntitlementStringSetCount + 1)
                .Select(index => index.ToString())));
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () =>
            EntitlementValue.FromStorage(SubscriptionEntitlementType.Enum, null, null, false, "pro", "[]"));
    }

    [Fact]
    public async Task Business_options_are_canonical_and_apply_enum_and_string_set_rules()
    {
        var enumFeature = new FeatureDefinition(
            "tier", new FixedLocalizableString("Tier"), SubscriptionEntitlementType.Enum);
        var setFeature = new FeatureDefinition(
            "regions", new FixedLocalizableString("Regions"), SubscriptionEntitlementType.StringSet);
        var provider = new FixedOptionsProvider(new[] { "zeta", "alpha" });

        Assert.Equal(new[] { "alpha", "zeta" },
            await provider.GetCanonicalOptionsAsync("alpha", enumFeature));
        await provider.ValidateOptionsAsync("alpha", enumFeature, EntitlementValue.Enum("alpha"));
        await provider.ValidateOptionsAsync(
            "alpha", setFeature, EntitlementValue.StringSet(new[] { "zeta", "alpha" }));
        Assert.Equal(SubscriptionErrorCodes.EntitlementOptionNotAllowed,
            (await Assert.ThrowsAsync<BusinessException>(() =>
                provider.ValidateOptionsAsync("alpha", enumFeature, EntitlementValue.Enum("missing")))).Code);
        Assert.Equal(SubscriptionErrorCodes.EntitlementOptionNotAllowed,
            (await Assert.ThrowsAsync<BusinessException>(() =>
                provider.ValidateOptionsAsync(
                    "alpha", setFeature, EntitlementValue.StringSet(new[] { "alpha", "missing" })))).Code);

        var freeForm = new FixedOptionsProvider(Array.Empty<string>());
        await freeForm.ValidateOptionsAsync(
            "alpha", setFeature, EntitlementValue.StringSet(new[] { "custom" }));
        Assert.Equal(SubscriptionErrorCodes.EntitlementOptionsRequired,
            (await Assert.ThrowsAsync<BusinessException>(() =>
                freeForm.GetCanonicalOptionsAsync("alpha", enumFeature))).Code);

        var manyOptions = new FixedOptionsProvider(
            Enumerable.Range(0, SubscriptionConsts.MaxEntitlementStringSetCount + 1)
                .Select(index => $"option-{index}")
                .ToArray());
        Assert.Equal(
            SubscriptionConsts.MaxEntitlementStringSetCount + 1,
            (await manyOptions.GetCanonicalOptionsAsync("alpha", enumFeature)).Count);

        var conflict = await Assert.ThrowsAsync<BusinessException>(() =>
            new ISubscriptionEntitlementOptionProvider[] { provider, freeForm }
                .GetCanonicalOptionsAsync("alpha", enumFeature));
        Assert.Equal(SubscriptionErrorCodes.EntitlementOptionProviderConflict, conflict.Code);
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

    [Fact]
    public void Default_result_source_is_not_an_implicit_subscription()
    {
        var planId = Guid.NewGuid();
        var finite = NumericEntitlementResult.FiniteDefaultPlan(planId, 20);
        Assert.Equal(EntitlementSource.DefaultPlan, finite.Source);
        Assert.Equal(planId, finite.PlanId);
        Assert.Null(finite.SubscriptionId);
        Assert.True(finite.Allows(20));
        Assert.False(finite.Allows(21));
        Assert.False(NumericEntitlementResult.NotGrantedDefaultPlan(planId).Allows(0));
        Assert.True(NumericEntitlementResult.FiniteDefaultPlan(planId, 0).Allows(0));
        Assert.True(NumericEntitlementResult.UnlimitedDefaultPlan(planId).Allows(long.MaxValue));
        Assert.False(BooleanEntitlementResult.FromDefaultPlan(planId, false).IsGranted);
        AssertCode(SubscriptionErrorCodes.InvalidEntitlementValue, () => NumericEntitlementResult.FiniteDefaultPlan(planId, -1));
    }

    [Fact]
    public void Enum_and_string_set_results_preserve_grant_source_and_use_ordinal_matching()
    {
        var subscriptionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var enumResult = EnumEntitlementResult.FromSubscription(subscriptionId, "pro", planId);
        Assert.True(enumResult.Matches("pro"));
        Assert.False(enumResult.Matches("PRO"));
        Assert.Equal(planId, enumResult.PlanId);

        var set = StringSetEntitlementResult.FromDefaultPlan(planId, new[] { "write", "read" });
        Assert.Equal(new[] { "read", "write" }, set.Values);
        Assert.True(set.Contains("read"));
        Assert.True(set.ContainsAll(new[] { "write", "read" }));
        Assert.False(set.Contains("READ"));
        Assert.Equal(EntitlementSource.DefaultPlan, set.Source);
        Assert.Empty(StringSetEntitlementResult.NoSubscription().Values);
    }

    [Fact]
    public void Default_product_cannot_be_unpublished_until_the_default_is_cleared()
    {
        var (product, plan) = Catalog();
        product.SetDefaultPlan(plan);
        Assert.Equal(plan.Id, product.DefaultPlanId);
        AssertCode(SubscriptionErrorCodes.DefaultPlanInUse, product.Withdraw);
        AssertCode(SubscriptionErrorCodes.DefaultPlanInUse, product.Archive);
        product.SetDefaultPlan(null);
        product.Withdraw();
        AssertCode(SubscriptionErrorCodes.InvalidDefaultPlan, () => product.SetDefaultPlan(plan));
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

    private sealed class FixedOptionsProvider : ISubscriptionEntitlementOptionProvider
    {
        private readonly IReadOnlyList<string> _options;

        public FixedOptionsProvider(IReadOnlyList<string> options) => _options = options;

        public bool CanProvide(string productCode, string featureKey) => true;

        public Task<IReadOnlyList<string>> GetOptionsAsync(
            string productCode, string featureKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_options);
    }
}
