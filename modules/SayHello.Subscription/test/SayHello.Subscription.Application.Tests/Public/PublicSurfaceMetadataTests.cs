using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.Hosting;
using SayHello.Subscription.Public.Catalog;
using SayHello.Subscription.Public.Entitlements;
using SayHello.Subscription.Public.Subscriptions;
using SayHello.Subscription.Public.Web.Pages.Public.Subscriptions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.Subscription.Public;

public class PublicSurfaceMetadataTests
{
    [Theory]
    [InlineData(typeof(SubscriptionCatalogController), "api/subscription/public", true)]
    [InlineData(typeof(MySubscriptionController), "api/subscription/public/mine", false)]
    [InlineData(typeof(CurrentUserEntitlementController), "api/subscription/public/entitlements/{productCode}", false)]
    public void Explicit_controllers_have_separate_named_public_routes_and_real_authorization(
        Type controller, string route, bool anonymous)
    {
        controller.GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe(route);
        controller.GetCustomAttribute<RemoteServiceAttribute>()!.Name
            .ShouldBe(SubscriptionPublicRemoteServiceConsts.RemoteServiceName);
        controller.IsDefined(typeof(AllowAnonymousAttribute)).ShouldBe(anonymous);
        controller.IsDefined(typeof(AuthorizeAttribute)).ShouldBe(!anonymous);
        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            method.GetCustomAttributes<HttpMethodAttribute>().Single().HttpMethods.ShouldBe(new[] { "GET" });
        }
    }

    [Theory]
    [InlineData(nameof(SubscriptionCatalogController.GetProductsAsync), "products")]
    [InlineData(nameof(SubscriptionCatalogController.GetProductAsync), "products/{id:guid}")]
    [InlineData(nameof(SubscriptionCatalogController.GetPlansAsync), "plans")]
    [InlineData(nameof(SubscriptionCatalogController.GetPlanAsync), "plans/{id:guid}")]
    [InlineData(nameof(SubscriptionCatalogController.GetBundlesAsync), "bundles")]
    [InlineData(nameof(SubscriptionCatalogController.GetBundleAsync), "bundles/{id:guid}")]
    public void Catalog_actions_have_unambiguous_routes(string action, string template) =>
        typeof(SubscriptionCatalogController).GetMethod(action)!.GetCustomAttribute<HttpGetAttribute>()!
            .Template.ShouldBe(template);

    [Fact]
    public void Razor_pages_and_application_services_have_real_authorization()
    {
        foreach (var type in new[] { typeof(MineModel), typeof(MySubscriptionAppService),
                     typeof(CurrentUserEntitlementAppService) })
        {
            type.IsDefined(typeof(AuthorizeAttribute)).ShouldBeTrue();
            type.IsDefined(typeof(AllowAnonymousAttribute)).ShouldBeFalse();
        }
        foreach (var type in new[] { typeof(PlansModel), typeof(BundlesModel), typeof(SubscriptionCatalogAppService) })
        {
            type.IsDefined(typeof(AllowAnonymousAttribute)).ShouldBeTrue();
        }
    }

    [Fact]
    public void Conventional_application_controllers_are_disabled_and_explicit_controllers_implement_contracts()
    {
        foreach (var type in new[] { typeof(SubscriptionCatalogAppService), typeof(MySubscriptionAppService),
                     typeof(CurrentUserEntitlementAppService) })
        {
            type.GetCustomAttribute<RemoteServiceAttribute>()!.IsEnabled.ShouldBeFalse();
        }
        typeof(ISubscriptionCatalogAppService).IsAssignableFrom(typeof(SubscriptionCatalogController)).ShouldBeTrue();
        typeof(IMySubscriptionAppService).IsAssignableFrom(typeof(MySubscriptionController)).ShouldBeTrue();
        typeof(ICurrentUserEntitlementAppService).IsAssignableFrom(typeof(CurrentUserEntitlementController)).ShouldBeTrue();
        typeof(SubscriptionPublicHttpApiClientModule).Assembly.GetName().Name
            .ShouldBe("SayHello.Subscription.Public.HttpApi.Client");
        SubscriptionPublicRemoteServiceConsts.RemoteServiceName.ShouldBe("SubscriptionPublic");
    }

    [Theory]
    [InlineData("Plans", "/subscriptions/plans/{id:guid?}")]
    [InlineData("Bundles", "/subscriptions/bundles/{id:guid?}")]
    [InlineData("Mine", "/subscriptions/mine/{id:guid?}")]
    public void Compiled_Razor_pages_have_explicit_multisegment_routes(string page, string route)
    {
        var item = typeof(MineModel).Assembly.GetCustomAttributes<RazorCompiledItemAttribute>()
            .Single(attribute => attribute.Identifier == $"/Pages/Public/Subscriptions/{page}.cshtml");
        item.Type.GetCustomAttributes<RazorCompiledItemMetadataAttribute>()
            .Single(attribute => attribute.Key == "RouteTemplate").Value.ShouldBe(route);
    }

    [Fact]
    public void English_and_Chinese_resources_are_embedded_and_have_matching_keys()
    {
        var assembly = typeof(SubscriptionPublicApplicationContractsModule).Assembly;
        JsonDocument Load(string culture)
        {
            var resource = assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith($".Localization.SubscriptionPublic.{culture}.json", StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(resource)!;
            return JsonDocument.Parse(stream);
        }
        using var en = Load("en");
        using var zh = Load("zh-Hans");
        en.RootElement.GetProperty("texts").EnumerateObject().Select(p => p.Name).OrderBy(n => n)
            .ShouldBe(zh.RootElement.GetProperty("texts").EnumerateObject().Select(p => p.Name).OrderBy(n => n));
        en.RootElement.GetProperty("texts").GetProperty("Unlimited").GetString().ShouldBe("Unlimited");
        zh.RootElement.GetProperty("texts").GetProperty("Unlimited").GetString().ShouldBe("无限制");
    }
}
