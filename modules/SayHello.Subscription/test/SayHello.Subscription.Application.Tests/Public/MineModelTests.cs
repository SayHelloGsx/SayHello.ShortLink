using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SayHello.Subscription.Public.Catalog;
using SayHello.Subscription.Public.Entitlements;
using SayHello.Subscription.Public.Subscriptions;
using SayHello.Subscription.Public.Web.Pages.Public.Subscriptions;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace SayHello.Subscription.Public;

public class MineModelTests
{
    private readonly IMySubscriptionAppService _subscriptions = Substitute.For<IMySubscriptionAppService>();
    private readonly ICurrentUserEntitlementAppService _entitlements =
        Substitute.For<ICurrentUserEntitlementAppService>();

    [Fact]
    public async Task Current_view_keeps_default_rights_and_assignment_counts_filters_and_paging_independent()
    {
        var productId = Guid.NewGuid();
        var assignment = new UserSubscriptionDto { Id = Guid.NewGuid(), ProductId = productId };
        var defaultPlan = new DefaultSubscriptionPlanDto { Id = Guid.NewGuid(), ProductId = productId };
        _subscriptions.GetListAsync(Arg.Any<GetMySubscriptionsInput>())
            .Returns(new PagedResultDto<UserSubscriptionDto>(17, new[] { assignment }));
        _entitlements.GetDefaultPlansAsync(Arg.Any<GetPublicCatalogInput>())
            .Returns(new PagedResultDto<DefaultSubscriptionPlanDto>(5, new[] { defaultPlan }));
        var model = Model("?Input.SkipCount=4&Input.Filter=paid&DefaultInput.SkipCount=2&DefaultInput.Filter=free");
        model.Input = new GetMySubscriptionsInput
        {
            CurrentOnly = true, ProductId = productId, Filter = "paid", Status = UserSubscriptionStatus.Expired,
            SkipCount = 4, MaxResultCount = 2
        };
        model.DefaultInput = new GetPublicCatalogInput { Filter = "free", SkipCount = 2, MaxResultCount = 2 };

        (await model.OnGetAsync()).ShouldBeOfType<PageResult>();
        model.Items.ShouldBe(new[] { assignment });
        model.DefaultPlans.ShouldBe(new[] { defaultPlan });
        model.Pagination.TotalCount.ShouldBe(17);
        model.DefaultPagination.TotalCount.ShouldBe(5);
        model.Pagination.NextUrl.ShouldNotBeNull();
        model.DefaultPagination.NextUrl.ShouldNotBeNull();
        model.Pagination.NextUrl!.ShouldContain("Input.SkipCount=6");
        model.Pagination.NextUrl.ShouldContain("DefaultInput.SkipCount=2");
        model.DefaultPagination.NextUrl!.ShouldContain("DefaultInput.SkipCount=4");
        model.DefaultPagination.NextUrl.ShouldContain("Input.SkipCount=4");
        model.DefaultPagination.NextUrl.ShouldContain("Input.Filter=paid");
        await _subscriptions.Received(1).GetListAsync(model.Input);
        await _entitlements.Received(1).GetDefaultPlansAsync(Arg.Is<GetPublicCatalogInput>(input =>
            input.ProductId == productId && input.Filter == "free" && input.SkipCount == 2 && input.MaxResultCount == 2));
    }

    [Fact]
    public async Task Default_product_filter_can_override_or_clear_the_assignment_product_scope()
    {
        _subscriptions.GetListAsync(Arg.Any<GetMySubscriptionsInput>())
            .Returns(new PagedResultDto<UserSubscriptionDto>(0, Array.Empty<UserSubscriptionDto>()));
        _entitlements.GetDefaultPlansAsync(Arg.Any<GetPublicCatalogInput>())
            .Returns(new PagedResultDto<DefaultSubscriptionPlanDto>(0, Array.Empty<DefaultSubscriptionPlanDto>()));
        var productId = Guid.NewGuid();
        var defaultProductId = Guid.NewGuid();
        var model = Model($"?Input.ProductId={productId}&Input.SkipCount=4&DefaultInput.ProductId={defaultProductId}&DefaultInput.SkipCount=2");
        model.Input.ProductId = productId;
        model.DefaultInput.ProductId = defaultProductId;

        await model.OnGetAsync();
        model.DefaultInput.ProductId.ShouldBe(defaultProductId);
        var clearUrl = model.DefaultProductUrl(null);
        clearUrl.ShouldContain($"Input.ProductId={productId}");
        clearUrl.ShouldContain("Input.SkipCount=4");
        clearUrl.ShouldContain("DefaultInput.ProductId=");
        clearUrl.ShouldNotContain("DefaultInput.SkipCount");
        model.DefaultProductUrl(productId).ShouldContain($"DefaultInput.ProductId={productId}");

        var cleared = Model($"?Input.ProductId={productId}&DefaultInput.ProductId=");
        cleared.Input.ProductId = productId;
        await cleared.OnGetAsync();
        cleared.DefaultInput.ProductId.ShouldBeNull();
    }

    [Fact]
    public async Task History_and_assignment_details_never_load_default_rights()
    {
        _subscriptions.GetListAsync(Arg.Any<GetMySubscriptionsInput>())
            .Returns(new PagedResultDto<UserSubscriptionDto>(3, Array.Empty<UserSubscriptionDto>()));
        var history = Model("?Input.CurrentOnly=false");
        history.Input.CurrentOnly = false;
        await history.OnGetAsync();
        history.Pagination.TotalCount.ShouldBe(3);
        history.DefaultPlans.ShouldBeEmpty();

        var detail = Model();
        detail.Id = Guid.NewGuid();
        var subscription = new UserSubscriptionDto { Id = detail.Id.Value };
        _subscriptions.GetAsync(detail.Id.Value).Returns(subscription);
        await detail.OnGetAsync();
        detail.Detail.ShouldBe(subscription);
        detail.DefaultPlans.ShouldBeEmpty();
        await _entitlements.DidNotReceive().GetDefaultPlansAsync(Arg.Any<GetPublicCatalogInput>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?Input.Filter=paid")]
    [InlineData("?DefaultInput.SkipCount=20")]
    public async Task Newly_bound_input_defaults_to_current_view_without_changing_the_API_input_default(string query)
    {
        _subscriptions.GetListAsync(Arg.Any<GetMySubscriptionsInput>())
            .Returns(new PagedResultDto<UserSubscriptionDto>(0, Array.Empty<UserSubscriptionDto>()));
        _entitlements.GetDefaultPlansAsync(Arg.Any<GetPublicCatalogInput>())
            .Returns(new PagedResultDto<DefaultSubscriptionPlanDto>(0, Array.Empty<DefaultSubscriptionPlanDto>()));
        var model = Model(query);
        model.Input = new GetMySubscriptionsInput();
        model.Input.CurrentOnly.ShouldBeFalse();

        await model.OnGetAsync();
        model.Input.CurrentOnly.ShouldBeTrue();
        await _entitlements.Received(1).GetDefaultPlansAsync(model.DefaultInput);
        new GetMySubscriptionsInput().CurrentOnly.ShouldBeFalse();
    }

    private MineModel Model(string query = "")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/subscriptions/mine";
        httpContext.Request.QueryString = new QueryString(query);
        return new MineModel(_subscriptions, _entitlements)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
    }
}
