using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Public.Entitlements;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.Subscription.Public;

public class CurrentUserEntitlementAppServiceTests
{
    private readonly PublicServiceTestContext _context = new();
    private readonly ISubscriptionEntitlementChecker _checker = Substitute.For<ISubscriptionEntitlementChecker>();
    private readonly CurrentUserEntitlementAppService _service;

    public CurrentUserEntitlementAppServiceTests()
    {
        _service = _context.Configure(new CurrentUserEntitlementAppService(_checker));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("ungranted")]
    [InlineData("zero")]
    [InlineData("unlimited")]
    public async Task Numeric_results_preserve_no_subscription_no_grant_zero_and_unlimited(string kind)
    {
        var id = System.Guid.NewGuid();
        var result = kind switch
        {
            "none" => NumericEntitlementResult.NoSubscription(),
            "ungranted" => NumericEntitlementResult.NotGranted(id),
            "zero" => NumericEntitlementResult.Finite(id, 0),
            _ => NumericEntitlementResult.Unlimited(id)
        };
        _checker.GetNumericAsync(_context.TenantId, _context.UserId, "one", "limit", Arg.Any<CancellationToken>())
            .Returns(result);
        var actual = await _service.GetNumericAsync("one", "limit");
        actual.Status.ShouldBe(result.Status);
        actual.SubscriptionId.ShouldBe(result.SubscriptionId);
        actual.IsGranted.ShouldBe(result.IsGranted);
        actual.IsUnlimited.ShouldBe(result.IsUnlimited);
        actual.Limit.ShouldBe(result.Limit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Boolean_queries_use_current_owner_and_preserve_grants(bool granted)
    {
        var id = System.Guid.NewGuid();
        _checker.GetBooleanAsync(_context.TenantId, _context.UserId, "one", "enabled", Arg.Any<CancellationToken>())
            .Returns(BooleanEntitlementResult.FromSubscription(id, granted));
        var actual = await _service.GetBooleanAsync("one", "enabled");
        actual.IsGranted.ShouldBe(granted);
        actual.Status.ShouldBe(granted ? EntitlementGrantStatus.Granted : EntitlementGrantStatus.NotGranted);
        actual.SubscriptionId.ShouldBe(id);
    }

    [Fact]
    public async Task Effective_snapshot_is_queried_through_checker_not_current_catalog()
    {
        var (_, product, plan) = _context.Catalog("one");
        var subscription = _context.Assign(product, plan);
        plan.Archive();
        product.Archive();
        _checker.FindEffectiveSubscriptionAsync(_context.TenantId, _context.UserId, "one", Arg.Any<CancellationToken>())
            .Returns(subscription);
        var actual = await _service.GetAsync("one");
        actual.HasEffectiveSubscription.ShouldBeTrue();
        actual.Subscription.ShouldNotBeNull();
        actual.Subscription.Id.ShouldBe(subscription.Id);
        actual.Subscription.Entitlements.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Missing_effective_subscription_is_explicit()
    {
        var result = await _service.GetAsync("one");
        result.HasEffectiveSubscription.ShouldBeFalse();
        result.Subscription.ShouldBeNull();
    }

    [Fact]
    public async Task An_assignment_expiring_during_the_request_is_not_an_effective_snapshot()
    {
        var (_, product, plan) = _context.Catalog("one");
        var subscription = _context.Assign(product, plan, _context.Now);
        _checker.FindEffectiveSubscriptionAsync(_context.TenantId, _context.UserId, "one", Arg.Any<CancellationToken>())
            .Returns(subscription);
        var result = await _service.GetAsync("one");
        result.HasEffectiveSubscription.ShouldBeFalse();
        result.Subscription.ShouldBeNull();
    }

    [Fact]
    public async Task Real_checker_keeps_missing_snapshot_features_ungranted_and_unknown_features_errors()
    {
        var (definition, product, plan) = _context.Catalog("one");
        plan.ReplaceEntitlements(definition, new Dictionary<string, EntitlementValue>
        {
            ["enabled"] = EntitlementValue.Boolean(false)
        });
        var subscription = _context.Assign(product, plan);
        var repository = Substitute.For<IUserSubscriptionRepository>();
        repository.FindEffectiveAsync(_context.TenantId, _context.UserId, product.Code, _context.Now,
            Arg.Any<CancellationToken>()).Returns(subscription);
        var definitions = Substitute.For<ISubscriptionDefinitionRegistry>();
        definitions.GetProduct(product.Code).Returns(definition);
        definitions.GetFeature(product.Code, Arg.Any<string>())
            .Returns(call => definition.GetFeature(call.ArgAt<string>(1)));
        var checker = new SubscriptionEntitlementChecker(definitions, repository,
            _context.CurrentTenant, _context.Clock);
        var service = _context.Configure(new CurrentUserEntitlementAppService(checker));

        (await service.GetNumericAsync("one", "limit")).Status.ShouldBe(EntitlementGrantStatus.NotGranted);
        (await service.GetBooleanAsync("one", "enabled")).IsGranted.ShouldBeFalse();
        var error = await Should.ThrowAsync<BusinessException>(() => service.GetBooleanAsync("one", "unknown"));
        error.Code.ShouldBe(SubscriptionErrorCodes.UnknownFeature);
        var wrongType = await Should.ThrowAsync<BusinessException>(() => service.GetBooleanAsync("one", "limit"));
        wrongType.Code.ShouldBe(SubscriptionErrorCodes.EntitlementTypeMismatch);
    }
}
