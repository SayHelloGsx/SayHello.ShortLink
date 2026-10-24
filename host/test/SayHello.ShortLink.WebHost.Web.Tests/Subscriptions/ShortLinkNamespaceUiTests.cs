using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinkNamespaces;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace SayHello.ShortLink.WebHost.Subscriptions;

public class ShortLinkNamespaceUiTests : IClassFixture<ShortLinkSubscriptionWebFactory>
{
    private const string NamespacePage = "/short-links/namespaces";
    private readonly ShortLinkSubscriptionWebFactory _factory;

    public ShortLinkNamespaceUiTests(ShortLinkSubscriptionWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Namespace_Page_Should_Require_Authentication_And_List_Permission(bool authenticated)
    {
        using var client = Client(authenticated ? ShortLinkSubscriptionWebFactory.NoPermissionOwner : null);
        using var response = await client.GetAsync(NamespacePage);
        response.StatusCode.ShouldBe(authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Missing_Benefits_Should_Leave_Default_Path_Available_Without_Implicit_Root()
    {
        using var client = Client(ShortLinkSubscriptionWebFactory.EnabledOwner);
        var document = await GetPageAsync(client, "/short-links");
        var selector = Node(document, "//select[@name='NewLink.NamespaceId']");
        selector.SelectNodes("./option").Count.ShouldBe(1);
        selector.SelectSingleNode("./option").GetAttributeValue("value", "missing").ShouldBeEmpty();
        HtmlEntity.DeEntitize(selector.InnerText).ShouldContain("/s/{code}");
        selector.Attributes.Contains("disabled").ShouldBeTrue();
        var root = Node(document, "//input[@name='NewLink.UseRootPath' and @type='checkbox']");
        root.Attributes.Contains("disabled").ShouldBeTrue();
        root.Attributes.Contains("checked").ShouldBeFalse();
        Node(document, "//button[@id='create-short-link-button']").Attributes.Contains("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Granted_Root_Path_Should_Render_An_Explicit_Unchecked_Option()
    {
        var owner = Guid.NewGuid();
        await AssignPlanAsync(owner, 1, rootLimit: 1);
        using var client = Client(owner);
        var document = await GetPageAsync(client, "/short-links");
        var root = Node(document, "//input[@name='NewLink.UseRootPath' and @type='checkbox']");
        root.Attributes.Contains("disabled").ShouldBeFalse();
        root.Attributes.Contains("checked").ShouldBeFalse();
        Node(document, "//select[@name='NewLink.NamespaceId']").Attributes.Contains("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_Should_Keep_Validation_Input_And_Existing_Namespaces_After_Quantity_Downgrade()
    {
        var owner = Guid.NewGuid();
        await AssignPlanAsync(owner, 2);
        using var client = Client(owner);
        var item = await CreateNamespaceAsync(client);
        var other = await CreateNamespaceAsync(client);
        await AssignPlanAsync(owner, 1);
        var initial = await GetPageAsync(client, "/short-links");
        using var response = await client.PostAsync("/short-links?handler=RefreshNamespaces",
            Form(initial, new Dictionary<string, string>
            {
                ["NewLink.Origin"] = item.Origin,
                ["NewLink.Title"] = "Keep namespace refresh input",
                ["NewLink.NamespaceId"] = item.Id.ToString(),
                ["NewLink.TargetUrl"] = string.Empty
            }));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = Parse(await response.Content.ReadAsStringAsync());
        Node(document, "//input[@name='NewLink.Title']").GetAttributeValue("value", "")
            .ShouldBe("Keep namespace refresh input");
        Node(document, "//span[@data-valmsg-for='NewLink.TargetUrl']").InnerText.ShouldNotBeNullOrWhiteSpace();
        var selector = Node(document, "//select[@name='NewLink.NamespaceId']");
        selector.Attributes.Contains("disabled").ShouldBeFalse();
        selector.SelectSingleNode($"./option[@value='{item.Id}']").ShouldNotBeNull();
        selector.SelectSingleNode($"./option[@value='{other.Id}']").ShouldNotBeNull();
        selector.SelectSingleNode($"./option[@value='{item.Id}' and @selected]").ShouldBeNull();
        selector.SelectSingleNode("./option[1]").GetAttributeValue("value", "missing").ShouldBeEmpty();

        using var lookup = await client.GetAsync("/short-links?handler=Namespaces&origin=" +
            Uri.EscapeDataString(item.Origin));
        lookup.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = System.Text.Json.JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("items").EnumerateArray()
            .Select(value => value.GetProperty("id").GetGuid())
            .ShouldBe([item.Id, other.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Feature_Downgrade_Should_Keep_Delete_Forms_And_Return_409_For_Stale_Stamp()
    {
        var owner = Guid.NewGuid();
        await AssignPlanAsync(owner, 1);
        using var client = Client(owner);
        var item = await CreateNamespaceAsync(client);
        await AssignPlanAsync(owner, 0);
        var document = await GetPageAsync(client, NamespacePage);
        Node(document, "//form[contains(@action, 'handler=Create')]//button[@type='submit']")
            .Attributes.Contains("disabled").ShouldBeTrue();
        var row = Node(document, $"//tr[@data-namespace-id='{item.Id}']");
        row.SelectSingleNode(".//form[contains(@class, 'delete-namespace-form')]").ShouldNotBeNull();
        row.SelectSingleNode(".//input[@name='concurrencyStamp']")
            .GetAttributeValue("value", "").ShouldBe(item.ConcurrencyStamp);
        row.SelectSingleNode(".//button[@type='submit']").Attributes.Contains("disabled").ShouldBeFalse();

        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        using var stale = await client.PostAsync(NamespacePage + "?handler=Delete",
            Form(document, new Dictionary<string, string>
            {
                ["id"] = item.Id.ToString(),
                ["concurrencyStamp"] = Guid.NewGuid().ToString("N")
            }));
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var error = System.Text.Json.JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        error.RootElement.GetProperty("error").GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();

        using var deleted = await client.PostAsync(NamespacePage + "?handler=Delete",
            Form(document, new Dictionary<string, string>
            {
                ["id"] = item.Id.ToString(),
                ["concurrencyStamp"] = item.ConcurrencyStamp
            }));
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await GetPageAsync(client, NamespacePage);
        after.DocumentNode.SelectSingleNode($"//tr[@data-namespace-id='{item.Id}']").ShouldBeNull();
    }

    [Fact]
    public async Task Namespace_Only_Permissions_Should_Allow_Cleanup_Without_Link_Capabilities()
    {
        var owner = Guid.NewGuid();
        await AssignPlanAsync(owner, 1, namespaceOnly: true);
        using var client = Client(owner);
        var item = await CreateNamespaceAsync(client);
        await AssignPlanAsync(owner, 0, namespaceOnly: true);
        var document = await GetPageAsync(client, NamespacePage);
        document.DocumentNode.SelectSingleNode("//input[@name='NewNamespace.Origin']").ShouldNotBeNull();
        document.DocumentNode.SelectSingleNode("//select[@name='NewNamespace.Origin']").ShouldBeNull();
        Node(document, $"//tr[@data-namespace-id='{item.Id}']")
            .SelectSingleNode(".//form[contains(@class, 'delete-namespace-form')]").ShouldNotBeNull();
        using var links = await client.GetAsync("/short-links");
        links.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Creation_Failure_Should_Preserve_Input_And_Delete_Blockers_Should_Remain_Visible_After_Downgrade()
    {
        var owner = Guid.NewGuid();
        await AssignPlanAsync(owner, 1);
        using var client = Client(owner);
        var initial = await GetPageAsync(client, NamespacePage);
        using var invalid = await client.PostAsync(NamespacePage + "?handler=Create",
            Form(initial, new Dictionary<string, string>
            {
                ["NewNamespace.Origin"] = ShortLinkSubscriptionTestData.DefaultOrigin,
                ["NewNamespace.Name"] = "admin"
            }));
        invalid.StatusCode.ShouldBe(HttpStatusCode.OK);
        var failed = Parse(await invalid.Content.ReadAsStringAsync());
        Node(failed, "//input[@name='NewNamespace.Name']").GetAttributeValue("value", "").ShouldBe("admin");
        Node(failed, "//select[@name='NewNamespace.Origin']/option[@selected]")
            .GetAttributeValue("value", "").ShouldBe(ShortLinkSubscriptionTestData.DefaultOrigin);
        HtmlEntity.DeEntitize(Node(failed, "//div[@role='alert']").InnerText)
            .ShouldContain("The namespace 'admin' is reserved");

        var item = await CreateNamespaceAsync(client);
        var input = ShortLinkSubscriptionTestData.NewLink();
        input.NamespaceId = item.Id;
        using var created = await client.PostAsJsonAsync("/api/short-link/public/links", input);
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        await AssignPlanAsync(owner, 0);
        var document = await GetPageAsync(client, NamespacePage);
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        using var blocked = await client.PostAsync(NamespacePage + "?handler=Delete",
            Form(document, new Dictionary<string, string>
            {
                ["id"] = item.Id.ToString(),
                ["concurrencyStamp"] = item.ConcurrencyStamp
            }));
        blocked.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var error = System.Text.Json.JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
        error.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe(ShortLinkErrorCodes.NamespaceInUse);
        error.RootElement.GetProperty("error").GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Public_Edit_And_Admin_List_Should_Display_Stored_Immutable_Path()
    {
        var item = _factory.DisabledLink;
        var path = string.IsNullOrEmpty(item.Namespace) ? $"/{item.Code}" : $"/{item.Namespace}/{item.Code}";
        using var owner = Client(ShortLinkSubscriptionWebFactory.DisabledOwner);
        var edit = await GetPageAsync(owner, $"/short-links/{item.Id}/edit");
        HtmlEntity.DeEntitize(edit.DocumentNode.InnerText).ShouldContain(path);
        edit.DocumentNode.SelectSingleNode("//input[@name='Input.NamespaceId']").ShouldBeNull();
        edit.DocumentNode.SelectSingleNode("//input[@name='Input.UseRootPath']").ShouldBeNull();
        Node(edit, "//input[@name='Input.ConcurrencyStamp']").GetAttributeValue("value", "")
            .ShouldBe(item.ConcurrencyStamp);
        using var admin = Client(ShortLinkSubscriptionWebFactory.Administrator);
        var list = await GetPageAsync(admin, "/admin/short-links");
        var link = Node(list, $"//a[@href='{item.Origin}{path}']");
        HtmlEntity.DeEntitize(link.ParentNode.InnerText).ShouldContain(path);
    }

    [Fact]
    public async Task Admin_Domain_Page_Should_Show_Empty_Namespace_References_And_Hide_Deletion()
    {
        var owner = Guid.NewGuid();
        var origin = $"https://ui-domain-{Guid.NewGuid():N}.example.test";
        await ShortLinkSubscriptionTestData.RunAsync(_factory.Services, null, owner, async services =>
        {
            var domain = await services.GetRequiredService<ShortLinkDomainManager>().CreateAsync(origin);
            await services.GetRequiredService<IShortLinkDomainRepository>().InsertAsync(domain, autoSave: true);
            var plan = await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 20, false, setDefault: false, domains: [origin],
                namespaceLimit: 1, rootLinkLimit: 0);
            await ShortLinkSubscriptionTestData.AssignAsync(services, owner, plan.Id);
            var permissions = services.GetRequiredService<IPermissionManager>();
            foreach (var permission in new[]
                     {
                         ShortLinkPublicPermissions.Namespaces.Default,
                         ShortLinkPublicPermissions.Namespaces.Create,
                         ShortLinkAdminPermissions.Default,
                         ShortLinkAdminPermissions.Domains.Default,
                         ShortLinkAdminPermissions.Domains.Delete
                     })
                await permissions.SetForUserAsync(owner, permission, true);
        });
        using var client = Client(owner);
        var before = await GetPageAsync(client, "/admin/short-links/domains");
        var unreferenced = Node(before, $"//tr[td/code[text()='{origin}']]");
        unreferenced.SelectSingleNode(".//form[contains(@class, 'delete-short-link-domain-form')]").ShouldNotBeNull();
        unreferenced.SelectSingleNode("./td[4]/span").ShouldBeNull();

        await CreateNamespaceAsync(client, origin);
        var after = await GetPageAsync(client, "/admin/short-links/domains");
        var referenced = Node(after, $"//tr[td/code[text()='{origin}']]");
        referenced.SelectSingleNode("./td[4]").InnerText.Trim().ShouldStartWith("0");
        referenced.SelectSingleNode("./td[4]/span").InnerText.ShouldBe("Referenced by namespaces");
        referenced.SelectSingleNode(".//form[contains(@class, 'delete-short-link-domain-form')]").ShouldBeNull();
    }

    [Fact]
    public async Task Reserved_Default_Setting_Should_Redisplay_Posted_Value_And_Field_Error()
    {
        var owner = Guid.NewGuid();
        await ShortLinkSubscriptionTestData.RunAsync(_factory.Services, null, owner, async services =>
        {
            var name = "namespace-settings-" + owner.ToString("N");
            var user = new IdentityUser(owner, name, name + "@example.test");
            user.SetEmailConfirmed(true);
            await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(user, autoSave: true);
            var permissions = services.GetRequiredService<IPermissionManager>();
            await permissions.SetForUserAsync(owner, ShortLinkAdminPermissions.Default, true);
            await permissions.SetForUserAsync(owner, ShortLinkAdminPermissions.ManageSettings, true);
        });
        using var client = Client(owner);
        var document = await GetPageAsync(client, "/admin/short-links/settings");
        var values = document.DocumentNode.SelectNodes("//input[starts-with(@name, 'Settings.')]")
            .ToDictionary(node => node.GetAttributeValue("name", ""), node => node.GetAttributeValue("value", ""));
        values["Settings.DefaultNamespace"] = "API";
        using var response = await client.PostAsync("/admin/short-links/settings", Form(document, values));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var failed = Parse(await response.Content.ReadAsStringAsync());
        Node(failed, "//input[@name='Settings.DefaultNamespace']").GetAttributeValue("value", "").ShouldBe("API");
        Node(failed, "//span[@data-valmsg-for='Settings.DefaultNamespace']").InnerText.ShouldNotBeNullOrWhiteSpace();
    }

    private Task AssignPlanAsync(Guid owner, long limit, bool namespaceOnly = false, long? rootLimit = 0) =>
        ShortLinkSubscriptionTestData.RunAsync(_factory.Services, null, owner, async services =>
        {
            var plan = await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 20, false, setDefault: false,
                namespaceLimit: limit, rootLinkLimit: rootLimit);
            await ShortLinkSubscriptionTestData.AssignAsync(services, owner, plan.Id);
            var permissions = services.GetRequiredService<IPermissionManager>();
            foreach (var permission in new[]
                     {
                         ShortLinkPublicPermissions.Namespaces.Default,
                         ShortLinkPublicPermissions.Namespaces.Create,
                         ShortLinkPublicPermissions.Namespaces.Delete
                     })
                await permissions.SetForUserAsync(owner, permission, true);
            if (!namespaceOnly)
            {
                await permissions.SetForUserAsync(owner, ShortLinkPublicPermissions.Default, true);
                await permissions.SetForUserAsync(owner, ShortLinkPublicPermissions.Create, true);
            }
        });

    private static async Task<ShortLinkNamespaceDto> CreateNamespaceAsync(HttpClient client, string? origin = null)
    {
        using var response = await client.PostAsJsonAsync("/api/short-link/public/namespaces",
            new CreateShortLinkNamespaceDto
            {
                Origin = origin ?? ShortLinkSubscriptionTestData.DefaultOrigin,
                Name = "ui-" + Guid.NewGuid().ToString("N")
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ShortLinkNamespaceDto>())!;
    }

    private HttpClient Client(Guid? owner)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://short.example.test")
        });
        if (owner.HasValue)
            client.DefaultRequestHeaders.Add(SubscriptionTestAuthenticationHandler.UserHeader, owner.Value.ToString("D"));
        return client;
    }

    private static async Task<HtmlDocument> GetPageAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, html);
        return Parse(html);
    }

    private static HtmlDocument Parse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }

    private static HtmlNode Node(HtmlDocument document, string xpath)
    {
        var node = document.DocumentNode.SelectSingleNode(xpath);
        node.ShouldNotBeNull($"{xpath}\n{HtmlEntity.DeEntitize(document.DocumentNode.InnerText)}");
        return node;
    }

    private static FormUrlEncodedContent Form(HtmlDocument document, Dictionary<string, string> values)
    {
        values["__RequestVerificationToken"] = Node(document, "//input[@name='__RequestVerificationToken']")
            .GetAttributeValue("value", "");
        return new FormUrlEncodedContent(values);
    }
}
