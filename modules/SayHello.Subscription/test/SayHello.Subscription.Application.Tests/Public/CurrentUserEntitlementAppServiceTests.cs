using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using NSubstitute;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Public.Entitlements;
using SayHello.Subscription.Public.Catalog;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace SayHello.Subscription.Public;

public class CurrentUserEntitlementAppServiceTests
{
    private readonly PublicServiceTestContext _context = new();
    private readonly ISubscriptionEntitlementChecker _checker = Substitute.For<ISubscriptionEntitlementChecker>();
    private readonly ISubscriptionDefinitionRegistry _definitions = Substitute.For<ISubscriptionDefinitionRegistry>();
    private readonly CurrentUserEntitlementAppService _service;

    public CurrentUserEntitlementAppServiceTests()
    {
        _service = CreateService(_checker);
        _checker.ResolveAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(EffectiveEntitlementContext.None());
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
        actual.Source.ShouldBe(result.Source);
        actual.PlanId.ShouldBe(result.PlanId);
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
        actual.Source.ShouldBe(EntitlementSource.Subscription);
    }

    [Fact]
    public async Task Typed_queries_forward_explicit_cancellation_tokens()
    {
        using var cancellation = new CancellationTokenSource();
        _checker.GetNumericAsync(
                _context.TenantId, _context.UserId, "one", "limit", cancellation.Token)
            .Returns(NumericEntitlementResult.Finite(Guid.NewGuid(), 20));
        _checker.GetBooleanAsync(
                _context.TenantId, _context.UserId, "one", "enabled", cancellation.Token)
            .Returns(BooleanEntitlementResult.FromSubscription(Guid.NewGuid(), true));

        await _service.GetNumericAsync("one", "limit", cancellation.Token);
        await _service.GetBooleanAsync("one", "enabled", cancellation.Token);

        await _checker.Received(1).GetNumericAsync(
            _context.TenantId, _context.UserId, "one", "limit", cancellation.Token);
        await _checker.Received(1).GetBooleanAsync(
            _context.TenantId, _context.UserId, "one", "enabled", cancellation.Token);
    }

    [Fact]
    public async Task Effective_snapshot_is_queried_through_checker_not_current_catalog()
    {
        var (_, product, plan) = _context.Catalog("one");
        var subscription = _context.Assign(product, plan);
        plan.Archive();
        product.Archive();
        _checker.ResolveAsync(_context.TenantId, _context.UserId, "one", Arg.Any<CancellationToken>())
            .Returns(EffectiveEntitlementContext.FromSubscription(subscription));
        var actual = await _service.GetAsync("one");
        actual.HasEffectiveSubscription.ShouldBeTrue();
        actual.Subscription.ShouldNotBeNull();
        actual.Subscription.Id.ShouldBe(subscription.Id);
        actual.Subscription.Entitlements.Count.ShouldBe(2);
        actual.Source.ShouldBe(EntitlementSource.Subscription);
        actual.PlanId.ShouldBe(plan.Id);
        actual.PlanCode.ShouldBe(subscription.PlanCode);
        actual.PlanName.ShouldBe(subscription.PlanName);
        actual.EntitlementsAreLive.ShouldBeFalse();
        actual.Entitlements.ShouldBe(actual.Subscription.Entitlements);
    }

    [Fact]
    public async Task Missing_effective_subscription_is_explicit()
    {
        var result = await _service.GetAsync("one");
        result.HasEffectiveSubscription.ShouldBeFalse();
        result.Subscription.ShouldBeNull();
        result.Source.ShouldBe(EntitlementSource.None);
        result.PlanId.ShouldBeNull();
        result.Entitlements.ShouldBeEmpty();
        result.EntitlementsAreLive.ShouldBeFalse();
    }

    [Fact]
    public async Task An_assignment_expiring_during_the_request_is_not_an_effective_snapshot()
    {
        var (_, product, plan) = _context.Catalog("one");
        var subscription = _context.Assign(product, plan, _context.Now);
        _checker.ResolveAsync(_context.TenantId, _context.UserId, "one", Arg.Any<CancellationToken>())
            .Returns(EffectiveEntitlementContext.FromSubscription(subscription), EffectiveEntitlementContext.None());
        var result = await _service.GetAsync("one");
        result.HasEffectiveSubscription.ShouldBeFalse();
        result.Subscription.ShouldBeNull();
        result.Source.ShouldBe(EntitlementSource.None);
        await _checker.Received(2).ResolveAsync(_context.TenantId, _context.UserId, "one", Arg.Any<CancellationToken>());
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
        repository.FindEffectiveAsync(_context.UserId, product.Code, _context.Now,
            Arg.Any<CancellationToken>()).Returns(subscription);
        var definitions = Substitute.For<ISubscriptionDefinitionRegistry>();
        definitions.GetProduct(product.Code).Returns(definition);
        definitions.GetFeature(product.Code, Arg.Any<string>())
            .Returns(call => definition.GetFeature(call.ArgAt<string>(1)));
        var checker = new SubscriptionEntitlementChecker(definitions, repository,
            _context.CurrentTenant, _context.Clock, Substitute.For<IDefaultSubscriptionPlanRepository>());
        var service = CreateService(checker);

        (await service.GetNumericAsync("one", "limit")).Status.ShouldBe(EntitlementGrantStatus.NotGranted);
        (await service.GetBooleanAsync("one", "enabled")).IsGranted.ShouldBeFalse();
        var error = await Should.ThrowAsync<BusinessException>(() => service.GetBooleanAsync("one", "unknown"));
        error.Code.ShouldBe(SubscriptionErrorCodes.UnknownFeature);
        var wrongType = await Should.ThrowAsync<BusinessException>(() => service.GetBooleanAsync("one", "limit"));
        wrongType.Code.ShouldBe(SubscriptionErrorCodes.EntitlementTypeMismatch);
    }

    [Fact]
    public async Task Default_summary_contains_live_plan_values_and_never_claims_an_assignment()
    {
        var (definition, product, plan) = SeedDefault();
        var summary = await _service.GetAsync(product.Code);
        summary.Source.ShouldBe(EntitlementSource.DefaultPlan);
        summary.PlanId.ShouldBe(plan.Id);
        summary.PlanCode.ShouldBe(plan.Code);
        summary.PlanName.ShouldBe(plan.Name);
        summary.HasEffectiveSubscription.ShouldBeFalse();
        summary.Subscription.ShouldBeNull();
        summary.EntitlementsAreLive.ShouldBeTrue();
        summary.Entitlements.Single(e => e.FeatureKey == "limit").Value.NumericValue.ShouldBe(20);
        summary.Entitlements.Single(e => e.FeatureKey == "enabled").Description.ShouldBe("Feature description");

        plan.ReplaceEntitlements(definition, new Dictionary<string, EntitlementValue>
        {
            ["limit"] = EntitlementValue.Numeric(30)
        });
        (await _service.GetAsync(product.Code)).Entitlements.Single().Value.NumericValue.ShouldBe(30);
        await _checker.DidNotReceive().FindEffectiveSubscriptionAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Summary_resolves_default_again_when_an_assignment_expires_during_the_request()
    {
        var (_, product, plan) = SeedDefault();
        var subscription = _context.Assign(product, plan, _context.Now);
        _checker.ResolveAsync(_context.TenantId, _context.UserId, product.Code, Arg.Any<CancellationToken>())
            .Returns(EffectiveEntitlementContext.FromSubscription(subscription),
                EffectiveEntitlementContext.FromDefaultPlan(new DefaultSubscriptionPlan(product, plan)));

        var result = await _service.GetAsync(product.Code);
        result.HasEffectiveSubscription.ShouldBeFalse();
        result.Subscription.ShouldBeNull();
        result.Source.ShouldBe(EntitlementSource.DefaultPlan);
        result.PlanId.ShouldBe(plan.Id);
        result.Entitlements.Single(e => e.FeatureKey == "limit").Value.NumericValue.ShouldBe(20);
    }

    [Theory]
    [InlineData("ungranted")]
    [InlineData("zero")]
    [InlineData("finite")]
    [InlineData("unlimited")]
    public async Task Default_feature_results_preserve_source_plan_and_value_semantics(string kind)
    {
        var planId = Guid.NewGuid();
        var numeric = kind switch
        {
            "ungranted" => NumericEntitlementResult.NotGrantedDefaultPlan(planId),
            "zero" => NumericEntitlementResult.FiniteDefaultPlan(planId, 0),
            "finite" => NumericEntitlementResult.FiniteDefaultPlan(planId, 20),
            _ => NumericEntitlementResult.UnlimitedDefaultPlan(planId)
        };
        _checker.GetNumericAsync(_context.TenantId, _context.UserId, "one", "limit", Arg.Any<CancellationToken>())
            .Returns(numeric);
        _checker.GetBooleanAsync(_context.TenantId, _context.UserId, "one", "enabled", Arg.Any<CancellationToken>())
            .Returns(BooleanEntitlementResult.FromDefaultPlan(planId, kind != "ungranted"));

        var actual = await _service.GetNumericAsync("one", "limit");
        actual.Source.ShouldBe(EntitlementSource.DefaultPlan);
        actual.PlanId.ShouldBe(planId);
        actual.SubscriptionId.ShouldBeNull();
        actual.Status.ShouldBe(numeric.Status);
        actual.Limit.ShouldBe(numeric.Limit);
        actual.IsUnlimited.ShouldBe(numeric.IsUnlimited);
        actual.IsGranted.ShouldBe(numeric.IsGranted);
        var boolean = await _service.GetBooleanAsync("one", "enabled");
        boolean.Source.ShouldBe(EntitlementSource.DefaultPlan);
        boolean.PlanId.ShouldBe(planId);
        boolean.SubscriptionId.ShouldBeNull();
        boolean.IsGranted.ShouldBe(kind != "ungranted");
    }

    [Fact]
    public async Task Default_listing_forwards_current_owner_and_independent_bounded_filters_without_repaging()
    {
        var (_, product, plan) = SeedDefault();
        _checker.GetDefaultPlansAsync(_context.TenantId, _context.UserId, Arg.Any<SubscriptionCatalogQuery>(),
            Arg.Any<CancellationToken>()).Returns(new SubscriptionPage<DefaultSubscriptionPlan>(7,
                new[] { new DefaultSubscriptionPlan(product, plan) }));
        var input = new GetPublicCatalogInput
        {
            Filter = "standard",
            ProductId = product.Id,
            Sorting = SubscriptionCatalogSort.NameDescending,
            SkipCount = 3,
            MaxResultCount = 2
        };

        var result = await _service.GetDefaultPlansAsync(input);
        result.TotalCount.ShouldBe(7);
        var item = result.Items.Single();
        item.Source.ShouldBe(EntitlementSource.DefaultPlan);
        item.Id.ShouldBe(plan.Id);
        item.ProductId.ShouldBe(product.Id);
        item.Entitlements.Single(e => e.FeatureKey == "limit").Value.NumericValue.ShouldBe(20);
        await _checker.Received(1).GetDefaultPlansAsync(_context.TenantId, _context.UserId,
            Arg.Is<SubscriptionCatalogQuery>(query => query.ProductId == product.Id &&
                query.PublishedOnly && query.State == null &&
                query.Filter == input.Filter && query.Sorting == input.Sorting &&
                query.SkipCount == 3 && query.MaxResultCount == 2), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("plan-withdrawn")]
    [InlineData("product-withdrawn")]
    [InlineData("different-tenant")]
    [InlineData("cleared")]
    public async Task Default_summary_and_listing_never_disclose_unavailable_catalog(string unavailable)
    {
        var (_, product, plan) = SeedDefault();
        _checker.GetDefaultPlansAsync(Arg.Any<Guid?>(), _context.UserId, Arg.Any<SubscriptionCatalogQuery>(),
            Arg.Any<CancellationToken>()).Returns(new SubscriptionPage<DefaultSubscriptionPlan>(1,
                new[] { new DefaultSubscriptionPlan(product, plan) }));
        if (unavailable == "plan-withdrawn") plan.Withdraw();
        if (unavailable == "product-withdrawn")
        {
            product.SetDefaultPlan(null);
            product.Withdraw();
        }
        if (unavailable == "cleared") product.SetDefaultPlan(null);
        if (unavailable == "different-tenant")
        {
            _context.CurrentTenant.Id.Returns(Guid.NewGuid());
            _checker.ResolveAsync(Arg.Any<Guid?>(), _context.UserId, product.Code, Arg.Any<CancellationToken>())
                .Returns(EffectiveEntitlementContext.FromDefaultPlan(new DefaultSubscriptionPlan(product, plan)));
        }

        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(product.Code));
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetDefaultPlansAsync(new GetPublicCatalogInput()));
    }

    [Fact]
    public async Task Real_checker_default_summary_features_and_listing_agree_without_subscription_writes()
    {
        var (_, product, plan) = SeedDefault();
        var subscriptions = Substitute.For<IUserSubscriptionRepository>();
        var defaults = Substitute.For<IDefaultSubscriptionPlanRepository>();
        var defaultPlan = new DefaultSubscriptionPlan(product, plan);
        defaults.FindAsync(product.Code, Arg.Any<CancellationToken>()).Returns(defaultPlan);
        defaults.GetPageAsync(Arg.Any<SubscriptionCatalogQuery>(), _context.UserId, _context.Now,
            Arg.Any<CancellationToken>()).Returns(new SubscriptionPage<DefaultSubscriptionPlan>(1, new[] { defaultPlan }));
        var service = CreateService(new SubscriptionEntitlementChecker(_definitions, subscriptions,
            _context.CurrentTenant, _context.Clock, defaults));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var summary = await service.GetAsync(product.Code);
            var numeric = await service.GetNumericAsync(product.Code, "limit");
            var boolean = await service.GetBooleanAsync(product.Code, "enabled");
            var listed = (await service.GetDefaultPlansAsync(new GetPublicCatalogInput())).Items.Single();
            summary.Source.ShouldBe(numeric.Source);
            summary.Source.ShouldBe(boolean.Source);
            summary.Source.ShouldBe(listed.Source);
            summary.PlanId.ShouldBe(numeric.PlanId);
            summary.PlanId.ShouldBe(listed.Id);
            summary.Entitlements.Single(e => e.FeatureKey == "limit").Value.NumericValue.ShouldBe(numeric.Limit);
            numeric.Limit.ShouldBe(20);
            numeric.SubscriptionId.ShouldBeNull();
            boolean.SubscriptionId.ShouldBeNull();
        }

        subscriptions.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "FindEffectiveAsync");
        defaults.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "FindAsync" ||
            call.GetMethodInfo().Name == "GetPageAsync");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(100L)]
    public async Task Real_checker_preserves_snapshot_values_without_borrowing_default_entitlements(long? limit)
    {
        var (definition, product, free) = SeedDefault();
        var paid = new SubscriptionPlan(Guid.NewGuid(), product, "paid", "Paid");
        var values = new Dictionary<string, EntitlementValue> { ["enabled"] = EntitlementValue.Boolean(false) };
        if (limit.HasValue) values["limit"] = EntitlementValue.Numeric(limit.Value);
        paid.ReplaceEntitlements(definition, values);
        paid.Publish(product, definition);
        var subscription = _context.Assign(product, paid);
        paid.ReplaceEntitlements(definition, new Dictionary<string, EntitlementValue>
        {
            ["enabled"] = EntitlementValue.Boolean(true),
            ["limit"] = EntitlementValue.Unlimited()
        });
        free.ReplaceEntitlements(definition, new Dictionary<string, EntitlementValue>
        {
            ["enabled"] = EntitlementValue.Boolean(true),
            ["limit"] = EntitlementValue.Numeric(30)
        });
        var subscriptions = Substitute.For<IUserSubscriptionRepository>();
        subscriptions.FindEffectiveAsync(_context.UserId, product.Code, _context.Now,
            Arg.Any<CancellationToken>()).Returns(subscription);
        var defaults = Substitute.For<IDefaultSubscriptionPlanRepository>();
        var service = CreateService(new SubscriptionEntitlementChecker(_definitions, subscriptions,
            _context.CurrentTenant, _context.Clock, defaults));

        var summary = await service.GetAsync(product.Code);
        var numeric = await service.GetNumericAsync(product.Code, "limit");
        var boolean = await service.GetBooleanAsync(product.Code, "enabled");
        summary.Source.ShouldBe(EntitlementSource.Subscription);
        summary.PlanId.ShouldBe(paid.Id);
        summary.EntitlementsAreLive.ShouldBeFalse();
        summary.Subscription.ShouldNotBeNull();
        summary.Subscription.Id.ShouldBe(subscription.Id);
        numeric.Source.ShouldBe(EntitlementSource.Subscription);
        numeric.PlanId.ShouldBe(paid.Id);
        numeric.SubscriptionId.ShouldBe(subscription.Id);
        numeric.Limit.ShouldBe(limit);
        numeric.IsUnlimited.ShouldBeFalse();
        numeric.Status.ShouldBe(limit.HasValue ? EntitlementGrantStatus.Granted : EntitlementGrantStatus.NotGranted);
        boolean.IsGranted.ShouldBeFalse();
        (summary.Entitlements.SingleOrDefault(e => e.FeatureKey == "limit")?.Value.NumericValue).ShouldBe(limit);
        defaults.ReceivedCalls().ShouldBeEmpty();
    }

    private CurrentUserEntitlementAppService CreateService(ISubscriptionEntitlementChecker checker) =>
        _context.Configure(new CurrentUserEntitlementAppService(checker, _definitions,
            Substitute.For<IStringLocalizerFactory>()));

    private (ProductDefinition Definition, SubscriptionProduct Product, SubscriptionPlan Plan) SeedDefault()
    {
        var (definition, product, plan) = _context.Catalog("one");
        plan.ReplaceEntitlements(definition, new Dictionary<string, EntitlementValue>
        {
            ["enabled"] = EntitlementValue.Boolean(true),
            ["limit"] = EntitlementValue.Numeric(20)
        });
        product.SetDefaultPlan(plan);
        _definitions.GetProduct(product.Code).Returns(definition);
        _definitions.GetFeature(product.Code, Arg.Any<string>())
            .Returns(call => definition.GetFeature(call.ArgAt<string>(1)));
        _checker.ResolveAsync(_context.TenantId, _context.UserId, product.Code, Arg.Any<CancellationToken>())
            .Returns(EffectiveEntitlementContext.FromDefaultPlan(new DefaultSubscriptionPlan(product, plan)));
        return (definition, product, plan);
    }
}
