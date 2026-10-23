using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinkNamespaces;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinkNamespaces;
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
    private const string Namespaces = "/api/short-link/public/namespaces";
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
        json.RootElement.GetProperty("isCustomCodeQuotaGranted").GetBoolean()
            .ShouldBeTrue();
        json.RootElement.GetProperty("isCustomCodeUnlimited").GetBoolean()
            .ShouldBeTrue();
        json.RootElement.GetProperty("remainingCustomCodes").ValueKind
            .ShouldBe(JsonValueKind.Null);
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
        var button = document.DocumentNode.SelectSingleNode("//button[@id='create-short-link-button']");
        button.ShouldNotBeNull();
        button.Attributes.Contains("disabled").ShouldBe(!unlimited);
        document.DocumentNode.SelectSingleNode("//tbody/tr/td[@colspan='5']").ShouldNotBeNull();
        if (unlimited)
            document.DocumentNode.InnerText.ShouldContain("Unlimited");
        else
            document.DocumentNode.SelectSingleNode("//div[@role='status']").ShouldNotBeNull();
    }

    [Fact]
    public async Task Missing_Custom_Code_Entitlement_Should_Disable_Only_The_Custom_Code_Input()
    {
        var owner = Guid.NewGuid();
        await ShortLinkSubscriptionTestData.RunAsync(
            _factory.Services,
            null,
            owner,
            async services =>
            {
                var plan = await ShortLinkSubscriptionTestData.CreatePlanAsync(
                    services,
                    2,
                    false,
                    setDefault: false,
                    customCodesUnlimited: false);
                await ShortLinkSubscriptionTestData.AssignAsync(
                    services,
                    owner,
                    plan.Id);
                foreach (var permission in new[]
                         {
                             ShortLinkPublicPermissions.Default,
                             ShortLinkPublicPermissions.Create
                         })
                {
                    await services.GetRequiredService<IPermissionManager>()
                        .SetForUserAsync(owner, permission, true);
                }
            });

        using var client = Client(owner);
        using var response = await client.GetAsync("/short-links");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = new HtmlDocument();
        document.LoadHtml(await response.Content.ReadAsStringAsync());

        var customCodeInput = document.DocumentNode.SelectSingleNode(
            "//input[@name='NewLink.CustomCode']");
        customCodeInput.ShouldNotBeNull();
        customCodeInput.Attributes.Contains("disabled").ShouldBeTrue();
        var createButton = document.DocumentNode.SelectSingleNode(
            "//form[contains(@action, 'handler=Create')]//button[@type='submit']");
        createButton.ShouldNotBeNull();
        createButton.Attributes.Contains("disabled").ShouldBeFalse();
        document.DocumentNode.InnerText.ShouldContain(
            "Leave this field empty to generate a code automatically.");
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
        var shortUrl = created.RootElement.GetProperty("shortUrl").GetString();
        using var visitor = Client(null);
        using var redirect = await visitor.GetAsync(shortUrl);
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

    [Fact]
    public async Task Missing_Namespace_And_Root_Benefits_Should_Not_Block_Default_Links()
    {
        using var client = Client(ShortLinkSubscriptionWebFactory.EnabledOwner);
        using var capabilities = await client.GetAsync(Links + "/capabilities");
        using var json = await JsonAsync(capabilities);
        json.RootElement.GetProperty("defaultNamespace").GetString().ShouldBe("s");
        json.RootElement.GetProperty("namespacesEnabled").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("isNamespaceQuotaGranted").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("isRootLinkQuotaGranted").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("remainingNamespaces").GetInt64().ShouldBe(0);
        json.RootElement.GetProperty("remainingRootLinks").GetInt64().ShouldBe(0);

        var createInput = ShortLinkSubscriptionTestData.NewLink();
        var link = await CreateLinkAsync(client, createInput);
        link.Namespace.ShouldBe("s");
        link.NamespaceId.ShouldBeNull();
        link.ShortUrl.ShouldBe($"{link.Origin}/s/{link.Code}");
        using var deniedNamespace = await client.PostAsJsonAsync(Namespaces,
            new CreateShortLinkNamespaceDto { Origin = createInput.Origin, Name = "missing-benefit" });
        await AssertBusinessErrorAsync(deniedNamespace, ShortLinkErrorCodes.NamespacesNotGranted);
        var rootInput = ShortLinkSubscriptionTestData.NewLink();
        rootInput.UseRootPath = true;
        using var deniedRoot = await client.PostAsJsonAsync(Links, rootInput);
        await AssertBusinessErrorAsync(deniedRoot, ShortLinkErrorCodes.RootLinkQuotaNotGranted);
    }

    [Fact]
    public async Task Namespace_Api_Should_Require_Permissions_And_Enforce_Ownership()
    {
        using var anonymous = Client(null);
        using var unauthenticated = await anonymous.GetAsync(Namespaces);
        unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var unauthorizedClient = Client(ShortLinkSubscriptionWebFactory.NoPermissionOwner);
        using var unauthorized = await unauthorizedClient.GetAsync(Namespaces);
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        await AssignNamespacePlanAsync(owner, true, 1, 1);
        await AssignNamespacePlanAsync(otherOwner, true, 1, 1);
        using var client = Client(owner);
        using var other = Client(otherOwner);
        var item = await CreateNamespaceAsync(client);
        using var owned = await client.GetAsync(
            Namespaces + "?origin=" + Uri.EscapeDataString(item.Origin));
        using var ownedJson = await JsonAsync(owned);
        var output = ownedJson.RootElement.GetProperty("items").EnumerateArray().Single();
        output.GetProperty("id").GetGuid().ShouldBe(item.Id);
        output.GetProperty("concurrencyStamp").GetString().ShouldBe(item.ConcurrencyStamp);
        output.TryGetProperty("ownerUserId", out _).ShouldBeFalse();
        output.TryGetProperty("tenantId", out _).ShouldBeFalse();
        using var otherList = await other.GetAsync(Namespaces);
        using var otherJson = await JsonAsync(otherList);
        otherJson.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);

        var input = ShortLinkSubscriptionTestData.NewLink();
        input.NamespaceId = item.Id;
        using var otherCreate = await other.PostAsJsonAsync(Links, input);
        await AssertBusinessErrorAsync(otherCreate, ShortLinkErrorCodes.NamespaceAccessDenied);
        using var otherDelete = await DeleteNamespaceAsync(other, item.Id, item.ConcurrencyStamp);
        await AssertBusinessErrorAsync(otherDelete, ShortLinkErrorCodes.NamespaceAccessDenied);
    }

    [Fact]
    public async Task Namespace_Root_And_Default_Paths_Should_Resolve_Independently_And_Invalidate_After_Status_Changes()
    {
        var owner = Guid.NewGuid();
        await AssignNamespacePlanAsync(owner, true, 1, 1);
        using var client = Client(owner);
        var item = await CreateNamespaceAsync(client);
        var code = "same" + Guid.NewGuid().ToString("N");
        var input = ShortLinkSubscriptionTestData.NewLink();
        input.CustomCode = code;
        input.NamespaceId = item.Id;
        input.TargetUrl = "https://destination.example.test/user";
        var userLink = await CreateLinkAsync(client, input);
        userLink.Namespace.ShouldBe(item.Name);
        userLink.NamespaceId.ShouldBe(item.Id);
        userLink.ShortUrl.ShouldBe($"{item.Origin}/{item.Name}/{code}");
        input.NamespaceId = null;
        input.UseRootPath = true;
        input.TargetUrl = "https://destination.example.test/root";
        var rootLink = await CreateLinkAsync(client, input);
        rootLink.Namespace.ShouldBeEmpty();
        rootLink.NamespaceId.ShouldBeNull();
        rootLink.ShortUrl.ShouldBe($"{item.Origin}/{code}");
        input.UseRootPath = false;
        input.TargetUrl = "https://destination.example.test/default";
        var defaultLink = await CreateLinkAsync(client, input);
        defaultLink.ShortUrl.ShouldBe($"{item.Origin}/s/{code}");

        using var visitor = Client(null);
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, userLink.ShortUrl);
        using var head = await visitor.SendAsync(headRequest);
        head.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        head.Headers.Location!.AbsoluteUri.ShouldBe(userLink.TargetUrl);
        head.Headers.CacheControl!.NoStore.ShouldBeTrue();
        await ShortLinkSubscriptionTestData.RunAsync(_factory.Services, null, owner, async services =>
        {
            var context = await services.GetRequiredService<IDbContextProvider<WebHostDbContext>>().GetDbContextAsync();
            (await context.ShortLinkVisits.CountAsync(visit => visit.ShortLinkId == userLink.Id)).ShouldBe(0);
            (await context.ShortLinks.SingleAsync(link => link.Id == userLink.Id)).TotalVisitCount.ShouldBe(0);
        });
        foreach (var link in new[] { userLink, rootLink, defaultLink })
        {
            using var redirect = await visitor.GetAsync(link.ShortUrl);
            redirect.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            redirect.Headers.Location!.AbsoluteUri.ShouldBe(link.TargetUrl);
            redirect.Headers.CacheControl!.NoStore.ShouldBeTrue();
        }
        using var currentResponse = await client.GetAsync($"{Links}/{userLink.Id}");
        using var current = await JsonAsync(currentResponse);
        using var status = await client.PutAsJsonAsync($"{Links}/{userLink.Id}/status",
            new SetShortLinkStatusDto
            {
                Status = ShortLinkStatus.Disabled,
                ConcurrencyStamp = current.RootElement.GetProperty("concurrencyStamp").GetString()!
            });
        status.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var gone = await visitor.GetAsync(userLink.ShortUrl);
        gone.StatusCode.ShouldBe(HttpStatusCode.Gone);
        using var rootStillActive = await visitor.GetAsync(rootLink.ShortUrl);
        rootStillActive.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        using var defaultStillActive = await visitor.GetAsync(defaultLink.ShortUrl);
        defaultStillActive.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        input.UseRootPath = true;
        input.CustomCode = "over" + Guid.NewGuid().ToString("N");
        using var fullRoot = await client.PostAsJsonAsync(Links, input);
        await AssertBusinessErrorAsync(fullRoot, ShortLinkErrorCodes.RootLinkQuotaExceeded);
        using var fullNamespace = await client.PostAsJsonAsync(Namespaces,
            new CreateShortLinkNamespaceDto { Origin = item.Origin, Name = "over" + Guid.NewGuid().ToString("N") });
        await AssertBusinessErrorAsync(fullNamespace, ShortLinkErrorCodes.NamespaceQuotaExceeded);
        using var quotaResponse = await client.GetAsync(Links + "/capabilities");
        using var quota = await JsonAsync(quotaResponse);
        quota.RootElement.GetProperty("usedLinks").GetInt64().ShouldBe(3);
        quota.RootElement.GetProperty("usedCustomCodes").GetInt64().ShouldBe(3);
        quota.RootElement.GetProperty("usedNamespaces").GetInt64().ShouldBe(1);
        quota.RootElement.GetProperty("usedRootLinks").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Quantity_Downgrade_Should_Keep_Namespace_Usable_And_Feature_Disable_Should_Keep_Management()
    {
        var owner = Guid.NewGuid();
        await AssignNamespacePlanAsync(owner, true, 2, 1);
        using var client = Client(owner);
        var item = await CreateNamespaceAsync(client);
        var empty = await CreateNamespaceAsync(client);
        await AssignNamespacePlanAsync(owner, true, 0, 1);
        var input = ShortLinkSubscriptionTestData.NewLink();
        input.NamespaceId = item.Id;
        var link = await CreateLinkAsync(client, input);
        using var deniedQuantity = await client.PostAsJsonAsync(Namespaces,
            new CreateShortLinkNamespaceDto { Origin = item.Origin, Name = "down" + Guid.NewGuid().ToString("N") });
        await AssertBusinessErrorAsync(deniedQuantity, ShortLinkErrorCodes.NamespaceQuotaExceeded);

        await AssignNamespacePlanAsync(owner, false, 0, 0);
        using var deniedFeature = await client.PostAsJsonAsync(Links, input);
        await AssertBusinessErrorAsync(deniedFeature, ShortLinkErrorCodes.NamespacesNotGranted);
        using var qr = await client.GetAsync($"{Links}/{link.Id}/qr-code");
        qr.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var qrJson = await JsonAsync(qr);
        qrJson.RootElement.GetProperty("contentType").GetString().ShouldBe("image/svg+xml");
        qrJson.RootElement.GetProperty("content").GetString().ShouldNotBeNullOrWhiteSpace();
        using var visitor = Client(null);
        using var redirect = await visitor.GetAsync(link.ShortUrl);
        redirect.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        using var inUse = await DeleteNamespaceAsync(client, item.Id, item.ConcurrencyStamp);
        await AssertBusinessErrorAsync(inUse, ShortLinkErrorCodes.NamespaceInUse);
        using var deleted = await DeleteNamespaceAsync(client, empty.Id, empty.ConcurrencyStamp);
        deleted.IsSuccessStatusCode.ShouldBeTrue();
        using var remaining = await client.GetAsync(Namespaces);
        using var remainingJson = await JsonAsync(remaining);
        remainingJson.RootElement.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Namespace_Delete_Should_Require_A_Valid_Body_And_Reject_A_Stale_Stamp()
    {
        var owner = Guid.NewGuid();
        await AssignNamespacePlanAsync(owner, true, 1, 1);
        using var client = Client(owner);
        var item = await CreateNamespaceAsync(client);
        using var missingBody = await client.DeleteAsync($"{Namespaces}/{item.Id}");
        missingBody.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        using var missingStampRequest = new HttpRequestMessage(HttpMethod.Delete, $"{Namespaces}/{item.Id}")
        {
            Content = JsonContent.Create(new { })
        };
        using var missingStamp = await client.SendAsync(missingStampRequest);
        missingStamp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var stale = await DeleteNamespaceAsync(client, item.Id, Guid.NewGuid().ToString("N"));
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var deleted = await DeleteNamespaceAsync(client, item.Id, item.ConcurrencyStamp);
        deleted.IsSuccessStatusCode.ShouldBeTrue();
        using var list = await client.GetAsync(Namespaces);
        using var json = await JsonAsync(list);
        json.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Second_Segment_Reserved_Code_Should_Work_And_Conflicting_Selection_Should_Fail_Validation()
    {
        var owner = Guid.NewGuid();
        await AssignNamespacePlanAsync(owner, false, 0, 0);
        using var client = Client(owner);
        var input = ShortLinkSubscriptionTestData.NewLink();
        input.CustomCode = "api";
        var link = await CreateLinkAsync(client, input);
        link.ShortUrl.ShouldBe($"{input.Origin}/s/api");
        using var visitor = Client(null);
        using var redirect = await visitor.GetAsync(link.ShortUrl);
        redirect.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        input.NamespaceId = Guid.NewGuid();
        input.UseRootPath = true;
        using var conflict = await client.PostAsJsonAsync(Links, input);
        conflict.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Actual_Two_Segment_System_Pages_Should_Have_Protected_Namespace_Prefixes()
    {
        var prefixes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PageActionDescriptor>() is not null)
            .Select(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty).Trim('/').Split('/'))
            .Where(segments => segments.Length == 2 &&
                               ShortLinkNamespaceFormat.IsValid(segments[0]) &&
                               ShortCodeFormat.IsValid(segments[1]))
            .Select(segments => segments[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        prefixes.ShouldNotBeEmpty();
        prefixes.Any(prefix => prefix.Equals("Identity", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        var options = _factory.Services.GetRequiredService<IOptions<ShortLinkNamespaceRouteOptions>>().Value;
        foreach (var prefix in prefixes)
            options.ReservedPrefixes.Contains(prefix).ShouldBeTrue($"Unprotected system route prefix: {prefix}");
    }

    private Task AssignNamespacePlanAsync(Guid owner, bool enabled, long namespaceLimit, long rootLinkLimit) =>
        ShortLinkSubscriptionTestData.RunAsync(_factory.Services, null, owner, async services =>
        {
            var plan = await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 20, false, setDefault: false,
                namespaces: enabled, namespaceLimit: namespaceLimit, rootLinkLimit: rootLinkLimit);
            await ShortLinkSubscriptionTestData.AssignAsync(services, owner, plan.Id);
            foreach (var permission in new[]
                     {
                         ShortLinkPublicPermissions.Default, ShortLinkPublicPermissions.Create,
                         ShortLinkPublicPermissions.Update, ShortLinkPublicPermissions.Delete,
                         ShortLinkPublicPermissions.Namespaces.Default,
                         ShortLinkPublicPermissions.Namespaces.Create,
                         ShortLinkPublicPermissions.Namespaces.Delete
                     })
                await services.GetRequiredService<IPermissionManager>().SetForUserAsync(owner, permission, true);
        });

    private static async Task<ShortLinkNamespaceDto> CreateNamespaceAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(Namespaces, new CreateShortLinkNamespaceDto
        {
            Origin = ShortLinkSubscriptionTestData.DefaultOrigin,
            Name = "Owned" + Guid.NewGuid().ToString("N")
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<ShortLinkNamespaceDto>();
        result.ShouldNotBeNull();
        return result;
    }

    private static async Task<ShortLinkDto> CreateLinkAsync(HttpClient client, CreateShortLinkDto input)
    {
        using var response = await client.PostAsJsonAsync(Links, input);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<ShortLinkDto>();
        result.ShouldNotBeNull();
        return result;
    }

    private static async Task<HttpResponseMessage> DeleteNamespaceAsync(
        HttpClient client, Guid id, string concurrencyStamp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{Namespaces}/{id}")
        {
            Content = JsonContent.Create(new DeleteShortLinkNamespaceDto { ConcurrencyStamp = concurrencyStamp })
        };
        return await client.SendAsync(request);
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
