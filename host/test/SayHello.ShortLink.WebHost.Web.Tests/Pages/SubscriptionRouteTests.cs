using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.WebHost.Pages;

public class SubscriptionRouteTests : WebHostWebTestBase
{
    [Theory]
    [InlineData("/subscriptions/plans")]
    [InlineData("/subscriptions/bundles")]
    [InlineData("/subscriptions/mine")]
    [InlineData("/admin/subscriptions/products")]
    [InlineData("/admin/subscriptions/plans")]
    [InlineData("/admin/subscriptions/bundles")]
    [InlineData("/admin/subscriptions/users")]
    public async Task Subscription_Pages_Should_Be_Composed_Into_Host(string route)
    {
        var response = await GetResponseAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Theory]
    [InlineData("/api/subscription/public/products?maxResultCount=10")]
    [InlineData("/api/subscription/public/plans?maxResultCount=10")]
    [InlineData("/api/subscription/public/bundles?maxResultCount=10")]
    [InlineData("/api/subscription/admin/products?maxResultCount=10")]
    [InlineData("/api/subscription/admin/plans?maxResultCount=10")]
    [InlineData("/api/subscription/admin/bundles?maxResultCount=10")]
    public async Task Subscription_Catalog_Apis_Should_Return_Paged_Json(string route)
    {
        var response = await GetResponseAsync(route);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        json.RootElement.GetProperty("totalCount").GetInt64().ShouldBeGreaterThanOrEqualTo(0);
        json.RootElement.GetProperty("items").ValueKind.ShouldBe(JsonValueKind.Array);
    }
}
