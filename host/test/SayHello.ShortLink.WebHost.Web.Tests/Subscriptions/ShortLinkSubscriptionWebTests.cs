using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using SayHello.ShortLink.WebHost.EntityFrameworkCore;
using SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace SayHello.ShortLink.WebHost.Subscriptions;

public class ShortLinkSubscriptionWebTests : IClassFixture<ShortLinkSubscriptionWebFactory>
{
    private const string Links = "/api/short-link/public/links";
    private readonly ShortLinkSubscriptionWebFactory _factory;

    public ShortLinkSubscriptionWebTests(ShortLinkSubscriptionWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Capabilities_Route_Should_Require_Authentication_And_Public_Link_Permission(bool authenticated)
    {
        using var client = Client(authenticated ? ShortLinkSubscriptionWebFactory.NoPermissionOwner : null);
        using var response = await client.GetAsync(Links + "/capabilities");
        response.StatusCode.ShouldBe(authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
        _factory.Services.GetRequiredService<IAuthorizationService>().ShouldBeAssignableTo<AbpAuthorizationService>();
        _factory.Services.GetRequiredService<IPermissionChecker>().ShouldBeAssignableTo<PermissionChecker>();
    }

    [Fact]
    public async Task Capabilities_Should_Ignore_Client_Supplied_User_And_Tenant_And_Return_Finite_Numbers()
    {
        using var client = Client(ShortLinkSubscriptionWebFactory.DisabledOwner);
        using var response = await client.GetAsync(Links +
            $"/capabilities?userId={ShortLinkSubscriptionWebFactory.EnabledOwner}&tenantId={Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await JsonAsync(response);
        json.RootElement.GetProperty("usedLinks").GetInt64().ShouldBeGreaterThanOrEqualTo(1);
        json.RootElement.GetProperty("isQuotaGranted").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("isUnlimited").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("maxLinks").GetInt64().ShouldBe(20);
        json.RootElement.GetProperty("remainingLinks").GetInt64()
            .ShouldBe(20 - json.RootElement.GetProperty("usedLinks").GetInt64());
        json.RootElement.GetProperty("statisticsEnabled").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Denied_And_Unlimited_Capabilities_Should_Have_Distinct_Json_Shapes(bool unlimited)
    {
        using var client = Client(unlimited
            ? ShortLinkSubscriptionWebFactory.UnlimitedOwner
            : ShortLinkSubscriptionWebFactory.DeniedOwner);
        using var response = await client.GetAsync(Links + "/capabilities");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await JsonAsync(response);
        json.RootElement.GetProperty("isQuotaGranted").GetBoolean().ShouldBe(unlimited);
        json.RootElement.GetProperty("isUnlimited").GetBoolean().ShouldBe(unlimited);
        json.RootElement.GetProperty("maxLinks").ValueKind.ShouldBe(JsonValueKind.Null);
        if (unlimited)
            json.RootElement.GetProperty("remainingLinks").ValueKind.ShouldBe(JsonValueKind.Null);
        else
            json.RootElement.GetProperty("remainingLinks").GetInt64().ShouldBe(0);
        json.RootElement.GetProperty("statisticsEnabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Every_Public_Link_Response_Should_Serialize_Disabled_Statistics_As_Null()
    {
        var storedCount = await ShortLinkSubscriptionTestData.RunAsync(
            _factory.Services, null, ShortLinkSubscriptionWebFactory.DisabledOwner,
            async services => (await services.GetRequiredService<IShortLinkRepository>()
                .GetAsync(_factory.DisabledLink.Id)).TotalVisitCount);
        storedCount.ShouldBe(7);
        using var client = Client(ShortLinkSubscriptionWebFactory.DisabledOwner);
        using var list = await client.GetAsync(Links);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var listJson = await JsonAsync(list);
        var persisted = listJson.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == _factory.DisabledLink.Id);
        AssertStatisticsHidden(persisted);
        using var get = await client.GetAsync($"{Links}/{_factory.DisabledLink.Id}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var getJson = await JsonAsync(get);
        AssertStatisticsHidden(getJson.RootElement);

        using var create = await client.PostAsJsonAsync(Links, ShortLinkSubscriptionTestData.NewLink());
        create.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var createJson = await JsonAsync(create);
        AssertStatisticsHidden(createJson.RootElement);
        var id = createJson.RootElement.GetProperty("id").GetGuid();
        using var update = await client.PutAsJsonAsync($"{Links}/{id}", new UpdateShortLinkDto
        {
            TargetUrl = "https://destination.example.test/updated",
            Title = "Updated without statistics",
            ConcurrencyStamp = createJson.RootElement.GetProperty("concurrencyStamp").GetString()!
        });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var updateJson = await JsonAsync(update);
        AssertStatisticsHidden(updateJson.RootElement);
        using var status = await client.PutAsJsonAsync($"{Links}/{id}/status", new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Disabled,
            ConcurrencyStamp = updateJson.RootElement.GetProperty("concurrencyStamp").GetString()!
        });
        status.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var statusJson = await JsonAsync(status);
        AssertStatisticsHidden(statusJson.RootElement);
    }

    [Theory]
    [InlineData("totalvisitcount")]
    [InlineData("totalvisitcount asc")]
    [InlineData("totalvisitcount desc")]
    [InlineData(" TOTALVISITCOUNT DESC ")]
    [InlineData("\ttotalvisitcount\tasc\t")]
    public async Task Disabled_Statistics_Should_Deny_Total_Visit_Ranking(string sorting)
    {
        using var client = Client(ShortLinkSubscriptionWebFactory.DisabledOwner);
        using var response = await client.GetAsync(Links + "?sorting=" + Uri.EscapeDataString(sorting));
        await AssertBusinessErrorAsync(response, ShortLinkErrorCodes.StatisticsNotGranted);
    }

    [Fact]
    public async Task Statistics_Entitlement_Should_Not_Replace_Ownership_Or_Statistics_Permission()
    {
        using var disabled = Client(ShortLinkSubscriptionWebFactory.DisabledOwner);
        using var denied = await disabled.GetAsync($"{Links}/{_factory.DisabledLink.Id}/statistics");
        await AssertBusinessErrorAsync(denied, ShortLinkErrorCodes.StatisticsNotGranted);
        using var enabled = Client(ShortLinkSubscriptionWebFactory.EnabledOwner);
        using var own = await enabled.GetAsync($"{Links}/{_factory.EnabledLink.Id}/statistics");
        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var ownJson = await JsonAsync(own);
        ownJson.RootElement.GetProperty("totalVisitCount").GetInt64().ShouldBe(7);
        using var otherOwner = await enabled.GetAsync($"{Links}/{_factory.DisabledLink.Id}/statistics");
        await AssertBusinessErrorAsync(otherOwner, ShortLinkErrorCodes.LinkAccessDenied);
        using var noPermission = Client(ShortLinkSubscriptionWebFactory.NoStatisticsPermissionOwner);
        using var noStatisticsPermission = await noPermission.GetAsync(
            $"{Links}/{_factory.NoStatisticsPermissionLink.Id}/statistics");
        noStatisticsPermission.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_Should_See_Real_Count_And_Keep_Statistics_Sorting_Without_A_Subscription()
    {
        using var client = Client(ShortLinkSubscriptionWebFactory.Administrator);
        using var response = await client.GetAsync(
            $"/api/short-link/admin/links?ownerUserId={ShortLinkSubscriptionWebFactory.DisabledOwner}&sorting=totalvisitcount%20desc");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await JsonAsync(response);
        json.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == _factory.DisabledLink.Id)
            .GetProperty("totalVisitCount").GetInt64().ShouldBe(7);
    }

    [Fact]
    public async Task Disabled_Statistics_Page_Should_Hide_Visits_And_Links_But_Keep_Management()
    {
        using var client = Client(ShortLinkSubscriptionWebFactory.DisabledOwner);
        using var response = await client.GetAsync("/short-links");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = new HtmlDocument();
        document.LoadHtml(await response.Content.ReadAsStringAsync());
        var row = document.DocumentNode.SelectNodes("//tbody/tr")!
            .Single(node => node.InnerText.Contains(_factory.DisabledLink.Code, StringComparison.Ordinal));
        row.SelectNodes("./td")!.Count.ShouldBe(5);
        row.SelectSingleNode($".//a[contains(@href, '/{_factory.DisabledLink.Id}/statistics')]").ShouldBeNull();
        row.SelectSingleNode($".//a[contains(@href, '/{_factory.DisabledLink.Id}/edit')]").ShouldNotBeNull();
        document.DocumentNode.SelectSingleNode("//div[contains(@class, 'card') and @aria-label]")!.InnerText.ShouldContain("20");
        using var detail = await client.GetAsync($"/short-links/{_factory.DisabledLink.Id}/statistics");
        detail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var detailDocument = new HtmlDocument();
        detailDocument.LoadHtml(await detail.Content.ReadAsStringAsync());
        detailDocument.DocumentNode.SelectSingleNode("//div[@role='alert']").ShouldNotBeNull();
        detailDocument.DocumentNode.SelectSingleNode("//div[@role='img']").ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_Denied_And_Unlimited_Pages_Should_Render_Quota_And_Create_State(bool unlimited)
    {
        using var client = Client(unlimited
            ? ShortLinkSubscriptionWebFactory.UnlimitedOwner
            : ShortLinkSubscriptionWebFactory.DeniedOwner);
        using var response = await client.GetAsync("/short-links");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = new HtmlDocument();
        document.LoadHtml(await response.Content.ReadAsStringAsync());
        var button = document.DocumentNode.SelectSingleNode("//form[contains(@action, 'handler=Create')]//button[@type='submit']");
        button.ShouldNotBeNull();
        button.Attributes.Contains("disabled").ShouldBe(!unlimited);
        document.DocumentNode.SelectSingleNode("//tbody/tr/td[@colspan='5']").ShouldNotBeNull();
        if (unlimited)
            document.DocumentNode.InnerText.ShouldContain("Unlimited");
        else
            document.DocumentNode.SelectSingleNode("//div[@role='status']").ShouldNotBeNull();
    }

    [Fact]
    public async Task Redirect_Should_Still_Record_When_Statistics_Are_Disabled_And_Recovery_Should_Reveal_History()
    {
        var owner = Guid.NewGuid();
        await ShortLinkSubscriptionTestData.RunAsync(_factory.Services, null, owner, async services =>
        {
            var plan = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 2, false, setDefault: false);
            await ShortLinkSubscriptionTestData.AssignAsync(services, owner, plan.Id);
            foreach (var permission in new[]
                     {
                         ShortLinkPublicPermissions.Default, ShortLinkPublicPermissions.Create,
                         ShortLinkPublicPermissions.ViewStatistics
                     })
                await services.GetRequiredService<IPermissionManager>().SetForUserAsync(owner, permission, true);
        });
        using var client = Client(owner);
        using var create = await client.PostAsJsonAsync(Links, ShortLinkSubscriptionTestData.NewLink());
        create.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var created = await JsonAsync(create);
        AssertStatisticsHidden(created.RootElement);
        var id = created.RootElement.GetProperty("id").GetGuid();
        var code = created.RootElement.GetProperty("code").GetString();
        using var visitor = Client(null);
        using var redirect = await visitor.GetAsync("/" + code);
        redirect.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        redirect.Headers.Location!.AbsoluteUri.ShouldBe("https://destination.example.test/path");
        using var denied = await client.GetAsync($"{Links}/{id}/statistics");
        await AssertBusinessErrorAsync(denied, ShortLinkErrorCodes.StatisticsNotGranted);

        await ShortLinkSubscriptionTestData.RunAsync(_factory.Services, null, owner, async services =>
        {
            var context = await services.GetRequiredService<IDbContextProvider<WebHostDbContext>>().GetDbContextAsync();
            (await context.ShortLinkVisits.CountAsync(visit => visit.ShortLinkId == id)).ShouldBe(1);
            (await context.ShortLinks.SingleAsync(link => link.Id == id)).TotalVisitCount.ShouldBe(1);
            var plan = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 2, true, setDefault: false);
            await ShortLinkSubscriptionTestData.AssignAsync(services, owner, plan.Id);
        });
        using var restored = await client.GetAsync($"{Links}/{id}/statistics");
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var statistics = await JsonAsync(restored);
        statistics.RootElement.GetProperty("totalVisitCount").GetInt64().ShouldBe(1);
        statistics.RootElement.GetProperty("daily").EnumerateArray()
            .Sum(day => day.GetProperty("visitCount").GetInt64()).ShouldBe(1);
    }

    [Fact]
    public async Task Direct_Create_Without_A_Subscription_Or_Default_Should_Be_Denied()
    {
        using var client = Client(ShortLinkSubscriptionWebFactory.DeniedOwner);
        using var response = await client.PostAsJsonAsync(Links, ShortLinkSubscriptionTestData.NewLink());
        await AssertBusinessErrorAsync(response, ShortLinkErrorCodes.LinkQuotaNotGranted);
    }

    private HttpClient Client(Guid? userId)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        if (userId.HasValue)
            client.DefaultRequestHeaders.Add(SubscriptionTestAuthenticationHandler.UserHeader, userId.Value.ToString("D"));
        return client;
    }

    private static void AssertStatisticsHidden(JsonElement item) =>
        item.GetProperty("totalVisitCount").ValueKind.ShouldBe(JsonValueKind.Null);

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task AssertBusinessErrorAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var json = await JsonAsync(response);
        json.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe(code);
    }
}
