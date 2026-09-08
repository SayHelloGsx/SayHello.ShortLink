using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using SayHello.Subscription;
using SayHello.Subscription.Entitlements;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class SubscriptionShortLinkCapabilityProviderTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sourceId = Guid.NewGuid();
    private readonly ISubscriptionEntitlementChecker _checker = Substitute.For<ISubscriptionEntitlementChecker>();
    private readonly SubscriptionShortLinkCapabilityProvider _provider;

    public SubscriptionShortLinkCapabilityProviderTests()
    {
        _provider = new SubscriptionShortLinkCapabilityProvider(_checker, Options.Create(BridgeTestDefinitions.Options()));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("subscription")]
    [InlineData("default")]
    public async Task Missing_subscription_or_quota_is_denied_not_unlimited_or_zero(string source)
    {
        Numeric(source switch
        {
            "subscription" => NumericEntitlementResult.NotGranted(_sourceId),
            "default" => NumericEntitlementResult.NotGrantedDefaultPlan(_sourceId),
            _ => NumericEntitlementResult.NoSubscription()
        });

        var quota = await _provider.GetQuotaAsync(null, _userId);

        _provider.IsQuotaExternallyManaged.ShouldBeTrue();
        quota.IsGranted.ShouldBeFalse();
        quota.IsUnlimited.ShouldBeFalse();
        quota.Limit.ShouldBeNull();
        _checker.ReceivedCalls().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false, 0L)]
    [InlineData(false, 1L)]
    [InlineData(false, 20L)]
    [InlineData(false, 100L)]
    [InlineData(false, long.MaxValue)]
    [InlineData(true, 0L)]
    [InlineData(true, 1L)]
    [InlineData(true, 20L)]
    [InlineData(true, 100L)]
    [InlineData(true, long.MaxValue)]
    public async Task Finite_quotas_preserve_zero_and_long_limits_without_setting_caps(bool fromDefault, long limit)
    {
        Numeric(fromDefault
            ? NumericEntitlementResult.FiniteDefaultPlan(_sourceId, limit)
            : NumericEntitlementResult.Finite(_sourceId, limit));

        var quota = await _provider.GetQuotaAsync(null, _userId);

        quota.IsGranted.ShouldBeTrue();
        quota.IsUnlimited.ShouldBeFalse();
        quota.Limit.ShouldBe(limit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_explicit_unlimited_grants_are_unlimited(bool fromDefault)
    {
        Numeric(fromDefault
            ? NumericEntitlementResult.UnlimitedDefaultPlan(_sourceId)
            : NumericEntitlementResult.Unlimited(_sourceId));

        var quota = await _provider.GetQuotaAsync(null, _userId);

        quota.IsGranted.ShouldBeTrue();
        quota.IsUnlimited.ShouldBeTrue();
        quota.Limit.ShouldBeNull();
    }

    [Theory]
    [InlineData("none", false)]
    [InlineData("subscription", false)]
    [InlineData("subscription", true)]
    [InlineData("default", false)]
    [InlineData("default", true)]
    public async Task Statistics_require_a_true_grant(string source, bool granted)
    {
        var result = source switch
        {
            "subscription" => BooleanEntitlementResult.FromSubscription(_sourceId, granted),
            "default" => BooleanEntitlementResult.FromDefaultPlan(_sourceId, granted),
            _ => BooleanEntitlementResult.NoSubscription()
        };
        _checker.GetBooleanAsync(Arg.Any<Guid?>(), _userId, BridgeTestDefinitions.ProductCode,
            BridgeTestDefinitions.StatisticsFeatureKey, Arg.Any<CancellationToken>()).Returns(result);

        (await _provider.IsStatisticsEnabledAsync(null, _userId)).ShouldBe(granted);
        _checker.ReceivedCalls().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typed_queries_forward_tenant_owner_configured_keys_and_cancellation(bool tenantScope)
    {
        Guid? tenantId = tenantScope ? Guid.NewGuid() : null;
        using var cancellation = new CancellationTokenSource();
        Numeric(NumericEntitlementResult.Finite(_sourceId, 20));
        _checker.GetBooleanAsync(tenantId, _userId, BridgeTestDefinitions.ProductCode,
            BridgeTestDefinitions.StatisticsFeatureKey, cancellation.Token)
            .Returns(BooleanEntitlementResult.FromSubscription(_sourceId, true));

        await _provider.GetQuotaAsync(tenantId, _userId, cancellation.Token);
        await _provider.IsStatisticsEnabledAsync(tenantId, _userId, cancellation.Token);

        await _checker.Received(1).GetNumericAsync(tenantId, _userId, BridgeTestDefinitions.ProductCode,
            BridgeTestDefinitions.QuotaFeatureKey, cancellation.Token);
        await _checker.Received(1).GetBooleanAsync(tenantId, _userId, BridgeTestDefinitions.ProductCode,
            BridgeTestDefinitions.StatisticsFeatureKey, cancellation.Token);
        _checker.ReceivedCalls().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Changes_are_read_on_each_query_without_a_capability_cache()
    {
        _checker.GetNumericAsync(null, _userId, BridgeTestDefinitions.ProductCode,
                BridgeTestDefinitions.QuotaFeatureKey, Arg.Any<CancellationToken>())
            .Returns(NumericEntitlementResult.Finite(_sourceId, 20),
                NumericEntitlementResult.Unlimited(_sourceId), NumericEntitlementResult.NoSubscription());
        _checker.GetBooleanAsync(null, _userId, BridgeTestDefinitions.ProductCode,
                BridgeTestDefinitions.StatisticsFeatureKey, Arg.Any<CancellationToken>())
            .Returns(BooleanEntitlementResult.FromSubscription(_sourceId, true),
                BooleanEntitlementResult.FromSubscription(_sourceId, false),
                BooleanEntitlementResult.FromDefaultPlan(_sourceId, true));

        (await _provider.GetQuotaAsync(null, _userId)).Limit.ShouldBe(20);
        (await _provider.GetQuotaAsync(null, _userId)).IsUnlimited.ShouldBeTrue();
        (await _provider.GetQuotaAsync(null, _userId)).IsGranted.ShouldBeFalse();
        (await _provider.IsStatisticsEnabledAsync(null, _userId)).ShouldBeTrue();
        (await _provider.IsStatisticsEnabledAsync(null, _userId)).ShouldBeFalse();
        (await _provider.IsStatisticsEnabledAsync(null, _userId)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("type")]
    [InlineData("storage")]
    [InlineData("cancellation")]
    public async Task Checker_errors_propagate_without_fallback_or_silent_grants(string failure)
    {
        Exception error = failure switch
        {
            "type" => new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch),
            "storage" => new InvalidOperationException("Storage unavailable"),
            _ => new OperationCanceledException()
        };
        _checker.GetNumericAsync(null, _userId, BridgeTestDefinitions.ProductCode,
                BridgeTestDefinitions.QuotaFeatureKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<NumericEntitlementResult>(error));
        _checker.GetBooleanAsync(null, _userId, BridgeTestDefinitions.ProductCode,
                BridgeTestDefinitions.StatisticsFeatureKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BooleanEntitlementResult>(error));

        (await Record.ExceptionAsync(() => _provider.GetQuotaAsync(null, _userId))).ShouldBeSameAs(error);
        (await Record.ExceptionAsync(() => _provider.IsStatisticsEnabledAsync(null, _userId))).ShouldBeSameAs(error);
    }

    private void Numeric(NumericEntitlementResult result) =>
        _checker.GetNumericAsync(Arg.Any<Guid?>(), _userId, BridgeTestDefinitions.ProductCode,
            BridgeTestDefinitions.QuotaFeatureKey, Arg.Any<CancellationToken>()).Returns(result);
}
