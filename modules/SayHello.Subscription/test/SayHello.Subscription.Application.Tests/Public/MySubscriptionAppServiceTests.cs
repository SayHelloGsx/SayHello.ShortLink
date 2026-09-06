using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Public.Subscriptions;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace SayHello.Subscription.Public;

public class MySubscriptionAppServiceTests
{
    private readonly PublicServiceTestContext _context = new();
    private readonly IUserSubscriptionRepository _repository = Substitute.For<IUserSubscriptionRepository>();
    private readonly MySubscriptionAppService _service;

    public MySubscriptionAppServiceTests()
    {
        _service = _context.Configure(new MySubscriptionAppService(_repository));
    }

    [Fact]
    public async Task List_always_scopes_filters_to_current_tenant_and_user()
    {
        UserSubscriptionQuery? observed = null;
        _repository.GetPageAsync(Arg.Any<UserSubscriptionQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                observed = call.Arg<UserSubscriptionQuery>();
                return new SubscriptionPage<UserSubscription>(12, Array.Empty<UserSubscription>());
            });
        var input = new GetMySubscriptionsInput
        {
            ProductId = Guid.NewGuid(), Status = UserSubscriptionStatus.Expired,
            CurrentOnly = true, Filter = "snapshot", SkipCount = 5, MaxResultCount = 7,
            Sorting = UserSubscriptionSort.ExpiresAt
        };
        (await _service.GetListAsync(input)).TotalCount.ShouldBe(12);
        observed.ShouldNotBeNull();
        observed.UserId.ShouldBe(_context.UserId);
        observed.TenantId.ShouldBe(_context.TenantId);
        observed.Now.ShouldBe(_context.Now);
        observed.ProductId.ShouldBe(input.ProductId);
        observed.Status.ShouldBe(input.Status);
        observed.CurrentOnly.ShouldBeTrue();
        observed.Filter.ShouldBe(input.Filter);
        observed.SkipCount.ShouldBe(5);
        observed.MaxResultCount.ShouldBe(7);
        observed.Sorting.ShouldBe(input.Sorting);
    }

    [Fact]
    public async Task Bundle_products_keep_independent_status_history_and_immutable_snapshots_after_catalog_changes()
    {
        var (firstDefinition, firstProduct, firstPlan) = _context.Catalog("one");
        var (_, secondProduct, secondPlan) = _context.Catalog("two");
        var bundle = new SubscriptionBundle(Guid.NewGuid(), _context.TenantId, "both", "Original bundle",
            new[] { firstPlan, secondPlan });
        bundle.Publish(new[] { firstPlan, secondPlan }, new[] { firstProduct, secondProduct });
        var assignment = Guid.NewGuid();
        var first = _context.Assign(firstProduct, firstPlan, _context.Now.AddDays(3), bundle, assignment);
        var second = _context.Assign(secondProduct, secondPlan, null, bundle, assignment);
        first.End(_context.Now, SubscriptionEndReason.Replaced);
        firstPlan.ReplaceEntitlements(firstDefinition, new Dictionary<string, EntitlementValue>
        {
            ["enabled"] = EntitlementValue.Boolean(false), ["limit"] = EntitlementValue.Unlimited()
        });
        firstPlan.UpdateDetails("Edited plan", null, 0);
        firstProduct.UpdateDetails("Edited product", null, 0);
        var replacement = _context.Assign(firstProduct, firstPlan, _context.Now.AddDays(8));
        firstPlan.Archive();
        firstProduct.Archive();
        secondPlan.Withdraw();
        secondProduct.Withdraw();
        bundle.Archive();
        _repository.GetPageAsync(Arg.Any<UserSubscriptionQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<UserSubscription>(3, new[] { replacement, second, first }));

        var result = await _service.GetListAsync(new GetMySubscriptionsInput());
        var history = result.Items.Single(s => s.Id == first.Id);
        history.Status.ShouldBe(UserSubscriptionStatus.Replaced);
        history.ProductName.ShouldBe("one product");
        history.PlanName.ShouldBe("one plan");
        history.BundleName.ShouldBe("Original bundle");
        history.Entitlements.Single(e => e.FeatureKey == "enabled").Value.BooleanValue.ShouldBe(true);
        history.Entitlements.Single(e => e.FeatureKey == "limit").Value.NumericValue.ShouldBe(0);
        var other = result.Items.Single(s => s.Id == second.Id);
        other.Status.ShouldBe(UserSubscriptionStatus.Active);
        other.ExpiresAt.ShouldBeNull();
        other.AssignmentId.ShouldBe(history.AssignmentId);
        var current = result.Items.Single(s => s.Id == replacement.Id);
        current.ProductId.ShouldBe(history.ProductId);
        current.Status.ShouldBe(UserSubscriptionStatus.Active);
        current.SourceBundleId.ShouldBeNull();
        current.Entitlements.Single(e => e.FeatureKey == "limit").Value.IsUnlimited.ShouldBeTrue();
        current.Entitlements.Single(e => e.FeatureKey == "enabled").Value.BooleanValue.ShouldBe(false);
    }

    [Fact]
    public async Task Current_slot_expiring_now_is_displayed_as_expired_not_active()
    {
        var (_, product, plan) = _context.Catalog("one");
        var subscription = _context.Assign(product, plan, _context.Now);
        _repository.GetAsync(_context.TenantId, subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        var result = await _service.GetAsync(subscription.Id);
        result.IsCurrent.ShouldBeTrue();
        result.Status.ShouldBe(UserSubscriptionStatus.Expired);
    }

    [Fact]
    public async Task Other_owners_are_not_disclosed_by_detail_or_list_even_if_repository_returns_their_row()
    {
        var (_, product, plan) = _context.Catalog("one");
        var other = _context.Assign(product, plan, userId: Guid.NewGuid());
        _repository.GetAsync(_context.TenantId, other.Id, Arg.Any<CancellationToken>()).Returns(other);
        _repository.GetPageAsync(Arg.Any<UserSubscriptionQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<UserSubscription>(1, new[] { other }));
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(other.Id));
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetListAsync(new GetMySubscriptionsInput()));
    }

    [Fact]
    public async Task Other_tenants_are_not_disclosed()
    {
        var (_, product, plan) = _context.Catalog("one");
        var subscription = _context.Assign(product, plan);
        _context.CurrentTenant.Id.Returns(Guid.NewGuid());
        _repository.GetAsync(Arg.Any<Guid?>(), subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(subscription.Id));
    }
}
