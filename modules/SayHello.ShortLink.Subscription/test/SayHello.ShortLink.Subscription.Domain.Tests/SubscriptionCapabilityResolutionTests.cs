using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using SayHello.Subscription;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class SubscriptionCapabilityResolutionTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ProductDefinition _definition = BridgeTestDefinitions.Product();
    private readonly SubscriptionShortLinkCapabilityProvider _provider;
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private UserSubscription? _subscription;
    private DefaultSubscriptionPlan? _default;

    public SubscriptionCapabilityResolutionTests()
    {
        var definitions = Substitute.For<ISubscriptionDefinitionRegistry>();
        definitions.GetProduct(_definition.Code).Returns(_definition);
        definitions.GetFeature(_definition.Code, Arg.Any<string>())
            .Returns(call => _definition.GetFeature(call.ArgAt<string>(1)));
        var subscriptions = Substitute.For<IUserSubscriptionRepository>();
        subscriptions.FindEffectiveAsync(_tenantId, _userId, _definition.Code,
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ => _subscription);
        var defaults = Substitute.For<IDefaultSubscriptionPlanRepository>();
        defaults.FindAsync(_tenantId, _definition.Code, Arg.Any<CancellationToken>()).Returns(_ => _default);
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(_tenantId);
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => _now);
        var checker = new SubscriptionEntitlementChecker(definitions, subscriptions, tenant, clock, defaults);
        _provider = new SubscriptionShortLinkCapabilityProvider(checker, Options.Create(BridgeTestDefinitions.Options()));
    }

    [Fact]
    public async Task No_subscription_or_default_denies_both_capabilities()
    {
        (await _provider.GetQuotaAsync(_tenantId, _userId)).IsGranted.ShouldBeFalse();
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Explicit_snapshots_do_not_change_with_the_catalog_or_fill_missing_features_from_defaults()
    {
        _default = Catalog(Values(20, true));
        _default.Product.SetDefaultPlan(_default.Plan);
        var explicitPlan = Catalog(new Dictionary<string, EntitlementValue>
        {
            [BridgeTestDefinitions.QuotaFeatureKey] = EntitlementValue.Numeric(100)
        });
        _subscription = Assign(explicitPlan);

        (await _provider.GetQuotaAsync(_tenantId, _userId)).Limit.ShouldBe(100);
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeFalse();

        explicitPlan.Plan.ReplaceEntitlements(_definition, Values(1, true));
        _default.Plan.ReplaceEntitlements(_definition, Values(1000, true));

        (await _provider.GetQuotaAsync(_tenantId, _userId)).Limit.ShouldBe(100);
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Missing_explicit_quota_does_not_fall_back_to_a_default_quota()
    {
        _default = Catalog(Values(20, true));
        _default.Product.SetDefaultPlan(_default.Plan);
        _subscription = Assign(Catalog(new Dictionary<string, EntitlementValue>
        {
            [BridgeTestDefinitions.StatisticsFeatureKey] = EntitlementValue.Boolean(false)
        }));

        (await _provider.GetQuotaAsync(_tenantId, _userId)).IsGranted.ShouldBeFalse();
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expiration_or_revocation_falls_back_to_live_defaults_then_denies_when_default_is_cleared(bool revoked)
    {
        _default = Catalog(Values(20, false));
        _default.Product.SetDefaultPlan(_default.Plan);
        _subscription = Assign(Catalog(Values(100, true)));
        (await _provider.GetQuotaAsync(_tenantId, _userId)).Limit.ShouldBe(100);
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeTrue();

        if (revoked)
        {
            _subscription.End(_now, SubscriptionEndReason.Revoked);
        }
        else
        {
            _now = _subscription.ExpiresAt!.Value;
        }

        (await _provider.GetQuotaAsync(_tenantId, _userId)).Limit.ShouldBe(20);
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeFalse();

        _default.Plan.ReplaceEntitlements(_definition, Values(50, true));
        (await _provider.GetQuotaAsync(_tenantId, _userId)).Limit.ShouldBe(50);
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeTrue();

        _default.Product.SetDefaultPlan(null);
        _default = null;
        (await _provider.GetQuotaAsync(_tenantId, _userId)).IsGranted.ShouldBeFalse();
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Replacing_default_with_partial_or_unlimited_plan_preserves_strict_grant_semantics()
    {
        _default = Catalog(Values(0, true));
        _default.Product.SetDefaultPlan(_default.Plan);
        var zero = await _provider.GetQuotaAsync(_tenantId, _userId);
        zero.IsGranted.ShouldBeTrue();
        zero.Limit.ShouldBe(0);

        _default.Plan.ReplaceEntitlements(_definition, new Dictionary<string, EntitlementValue>
        {
            [BridgeTestDefinitions.QuotaFeatureKey] = EntitlementValue.Unlimited()
        });
        (await _provider.GetQuotaAsync(_tenantId, _userId)).IsUnlimited.ShouldBeTrue();
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeFalse();

        var replacement = new SubscriptionPlan(Guid.NewGuid(), _default.Product, "replacement", "Replacement");
        replacement.ReplaceEntitlements(_definition, new Dictionary<string, EntitlementValue>
        {
            [BridgeTestDefinitions.StatisticsFeatureKey] = EntitlementValue.Boolean(true)
        });
        replacement.Publish(_default.Product, _definition);
        _default.Product.SetDefaultPlan(replacement);
        _default = new DefaultSubscriptionPlan(_default.Product, replacement);

        (await _provider.GetQuotaAsync(_tenantId, _userId)).IsGranted.ShouldBeFalse();
        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBeTrue();
    }

    private DefaultSubscriptionPlan Catalog(IReadOnlyDictionary<string, EntitlementValue> values)
    {
        var product = new SubscriptionProduct(Guid.NewGuid(), _tenantId, _definition, "Product");
        product.Publish();
        var plan = new SubscriptionPlan(Guid.NewGuid(), product, "plan", "Plan");
        plan.ReplaceEntitlements(_definition, values);
        plan.Publish(product, _definition);
        return new DefaultSubscriptionPlan(product, plan);
    }

    private UserSubscription Assign(DefaultSubscriptionPlan source) => new(
        Guid.NewGuid(), _userId, source.Product, source.Plan,
        source.Plan.Entitlements.Select(value =>
            new EntitlementSnapshotData(value.FeatureKey, value.FeatureKey, value.ToValue())).ToArray(),
        _now, _now.AddHours(1), Guid.NewGuid());

    private static IReadOnlyDictionary<string, EntitlementValue> Values(long quota, bool statistics) =>
        new Dictionary<string, EntitlementValue>
        {
            [BridgeTestDefinitions.QuotaFeatureKey] = EntitlementValue.Numeric(quota),
            [BridgeTestDefinitions.StatisticsFeatureKey] = EntitlementValue.Boolean(statistics)
        };
}
