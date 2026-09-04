using System.Net;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.WebHost.Pages;

public class Index_Tests : WebHostWebTestBase
{
    [Fact]
    public async Task Welcome_Page()
    {
        var response = await GetResponseAsStringAsync("/");
        response.ShouldContain("ShortLink");
    }

    [Theory]
    [InlineData("/short-links")]
    [InlineData("/admin/short-links")]
    [InlineData("/admin/short-links/blocked-domains")]
    [InlineData("/admin/short-links/settings")]
    public async Task Split_Web_Routes_Should_Be_Registered(string route)
    {
        var response = await GetResponseAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/short-link/public/links?maxResultCount=10")]
    [InlineData("/api/short-link/admin/links?maxResultCount=10")]
    public async Task Split_Api_Routes_Should_Be_Registered(string route)
    {
        var response = await GetResponseAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_Short_Code_Should_Return_NotFound()
    {
        var response = await GetResponseAsync("/NoSuchCode", HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("404");
    }
}
