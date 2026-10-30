using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SayHello.LiquidGlassTheme;
using SayHello.LiquidGlassTheme.Components.MainMenu;
using GlassTheme = SayHello.LiquidGlassTheme.LiquidGlassTheme;
using Shouldly;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.Security.Claims;
using Volo.Abp.UI.Navigation;
using Xunit;

namespace SayHello.ShortLink.WebHost.Pages;

public class LiquidGlassThemeContractTests
{
    [Theory]
    [InlineData(StandardLayouts.Application, GlassTheme.ApplicationLayout)]
    [InlineData(StandardLayouts.Public, GlassTheme.ApplicationLayout)]
    [InlineData(StandardLayouts.Account, GlassTheme.AccountLayout)]
    [InlineData(StandardLayouts.Empty, GlassTheme.EmptyLayout)]
    public void Standard_Layouts_Should_Resolve(string name, string expected)
    {
        var theme = new GlassTheme(Options.Create(new LiquidGlassThemeOptions()));
        theme.GetLayout(name).ShouldBe(expected);
    }

    [Fact]
    public void Layout_Overrides_And_Fallback_Should_Follow_Abp_Contract()
    {
        var options = new LiquidGlassThemeOptions();
        options.Layouts[StandardLayouts.Account] = "~/Custom/Account.cshtml";
        options.Layouts["Custom"] = "~/Custom/Layout.cshtml";
        var theme = new GlassTheme(Options.Create(options));

        theme.GetLayout(StandardLayouts.Account).ShouldBe("~/Custom/Account.cshtml");
        theme.GetLayout("Custom", false).ShouldBe("~/Custom/Layout.cshtml");
        theme.GetLayout("Unknown").ShouldBe(GlassTheme.ApplicationLayout);
        theme.GetLayout("Unknown", false).ShouldBeNull();
    }

    [Fact]
    public void Menu_Selection_Should_Propagate_And_Preserve_Item_Metadata()
    {
        var later = new ApplicationMenuItem("Later", "Later", url: "/short-links/later", order: 20);
        var selected = new ApplicationMenuItem("Selected", "Selected", url: "~/short-links", order: 10,
            icon: "fa fa-link", target: "_blank", elementId: "selected-menu", cssClass: "custom-menu");
        var parent = new ApplicationMenuItem("Parent", "Parent").AddItem(later).AddItem(selected);
        var model = LiquidGlassMenuItemViewModel.Create(parent, "Selected", "/different", ResolveUrl);

        model.IsActive.ShouldBeTrue();
        model.Children.Select(child => child.Item.Name).ShouldBe(new[] { "Selected", "Later" });
        model.Children[0].IsActive.ShouldBeTrue();
        model.Children[0].IsTopLevel.ShouldBeFalse();
        model.Children[0].Url.ShouldBe("/short-links");
        model.Children[0].Item.ShouldBeSameAs(selected);
        model.Children[0].Item.ElementId.ShouldBe("selected-menu");
        model.Children[0].Item.Target.ShouldBe("_blank");
    }

    [Fact]
    public void Menu_Url_Fallback_Should_Use_Path_Boundaries_And_Respect_Disabled_Items()
    {
        var item = new ApplicationMenuItem("Links", "Links", url: "/short-links?filter=all#list");
        LiquidGlassMenuItemViewModel.Create(item, null, "/short-links/edit", ResolveUrl).IsActive.ShouldBeTrue();
        LiquidGlassMenuItemViewModel.Create(item, null, "/short-links-other", ResolveUrl).IsActive.ShouldBeFalse();
        LiquidGlassMenuItemViewModel.Create(item, "Different", "/short-links", ResolveUrl).IsActive.ShouldBeFalse();
        item.IsDisabled = true;
        LiquidGlassMenuItemViewModel.Create(item, "Links", "/short-links", ResolveUrl).IsActive.ShouldBeFalse();
    }

    private static string ResolveUrl(string url) => url.StartsWith("~/", StringComparison.Ordinal) ? url[1..] : url;
}

public class LiquidGlassThemeTests : WebHostWebTestBase
{
    public LiquidGlassThemeTests()
    {
        Server.PreserveExecutionContext = true;
        Client.Dispose();
        Client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public void Host_Should_Select_LiquidGlass_And_Keep_Default_Options()
    {
        GetRequiredService<IOptions<AbpThemingOptions>>().Value.DefaultThemeName.ShouldBe(GlassTheme.Name);
        GetRequiredService<GlassTheme>().GetLayout(StandardLayouts.Application).ShouldBe(GlassTheme.ApplicationLayout);
        var options = GetRequiredService<IOptions<LiquidGlassThemeOptions>>().Value;
        options.DefaultColorMode.ShouldBe(LiquidGlassColorMode.System);
        options.EnableWebGl.ShouldBeTrue();
        options.MaxGlassSurfaces.ShouldBe(24);
    }

    [Fact]
    public async Task Application_Page_Should_Keep_Abp_Infrastructure_And_Real_Content()
    {
        var html = await GetResponseAsStringAsync("/");
        var document = Parse(html);
        document.DocumentNode.SelectSingleNode("//html[@data-lgt-module-url='/Themes/LiquidGlass/liquid-glass.mjs']").ShouldNotBeNull();
        document.DocumentNode.SelectSingleNode("//header[@data-lgt-header]").ShouldNotBeNull();
        document.DocumentNode.SelectSingleNode("//nav[@data-lgt-navigation]").ShouldNotBeNull();
        document.DocumentNode.SelectSingleNode("//main[@id='lgt-main']").ShouldNotBeNull();
        document.DocumentNode.SelectSingleNode("//section[@data-lgt-glass='panel']").ShouldNotBeNull();
        document.DocumentNode.SelectSingleNode("//*[@data-lgt-scene]//input|//*[@data-lgt-scene]//button|//*[@data-lgt-scene]//a").ShouldBeNull();
        html.ShouldContain("/Abp/ApplicationLocalizationScript");
        html.ShouldContain("/Abp/ApplicationConfigurationScript");
        html.ShouldContain("/Abp/ServiceProxyScript");
        html.ShouldContain("/short-links");
        html.ShouldNotContain("/Themes/Lepton");
        html.ShouldNotContain("aspnetcore.mvc.ui.theme.leptonxlite");
    }

    [Fact]
    public async Task Account_Pages_Should_Keep_Their_Forms_And_Antiforgery()
    {
        using var anonymous = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity()));
        foreach (var route in new[] { "/Account/Login", "/Account/Register" })
        {
            var html = await GetResponseAsStringAsync(route);
            var document = Parse(html);
            document.DocumentNode.SelectSingleNode("//*[@class and contains(@class, 'lgt-account-panel')]").ShouldNotBeNull();
            document.DocumentNode.SelectSingleNode("//form[@method='post']").ShouldNotBeNull();
            document.DocumentNode.SelectSingleNode("//input[@name='__RequestVerificationToken']").ShouldNotBeNull();
            document.DocumentNode.SelectSingleNode("//nav[@data-lgt-navigation]").ShouldBeNull();
        }
        (await GetResponseAsStringAsync("/Account/EmailConfirmationSent")).ShouldContain("lgt-account-panel");
    }

    [Fact]
    public async Task Anonymous_Login_Link_Should_Preserve_Return_Url()
    {
        using var anonymous = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity()));
        var document = Parse(await GetResponseAsStringAsync("/?source=theme-test"));
        var link = document.DocumentNode.SelectSingleNode("//a[contains(@class, 'lgt-login-link')]");
        link.ShouldNotBeNull();
        var uri = new Uri(Client.BaseAddress!, HtmlEntity.DeEntitize(link.GetAttributeValue("href", "")));
        uri.AbsolutePath.ShouldBe("/Account/Login");
        QueryHelpers.ParseQuery(uri.Query)["returnUrl"].ToString().ShouldBe("/?source=theme-test");
    }

    [Fact]
    public async Task Language_Menu_Should_Keep_Return_Url_And_Active_Main_Menu()
    {
        var document = Parse(await GetResponseAsStringAsync("/short-links?source=theme-test"));
        var languages = document.DocumentNode.SelectNodes("//details[contains(@class, 'lgt-language-menu')]//a");
        languages.ShouldNotBeNull();
        languages.Count.ShouldBeGreaterThanOrEqualTo(2);
        foreach (var link in languages)
        {
            var uri = new Uri(Client.BaseAddress!, HtmlEntity.DeEntitize(link.GetAttributeValue("href", "")));
            QueryHelpers.ParseQuery(uri.Query)["returnUrl"].ToString().ShouldBe("/short-links?source=theme-test");
        }
        document.DocumentNode.SelectSingleNode("//nav[@data-lgt-navigation]//a[@href='/short-links' and @aria-current='page']").ShouldNotBeNull();
    }

    [Theory]
    [InlineData("liquid-glass.css")]
    [InlineData("liquid-glass-init.js")]
    [InlineData("liquid-glass-theme.js")]
    [InlineData("liquid-glass.mjs")]
    [InlineData("liquid-glass-fonts.mjs")]
    [InlineData("assets/day.svg")]
    [InlineData("assets/night.svg")]
    [InlineData("assets/link-sculpture.svg")]
    [InlineData("assets/favicon.svg")]
    [InlineData("vendor/liquidglass.js")]
    [InlineData("vendor/NOTICE.txt")]
    public async Task Embedded_Theme_Asset_Should_Be_Served(string asset)
    {
        using var response = await Client.GetAsync("/Themes/LiquidGlass/" + asset);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Asset: {asset}; redirect: {response.Headers.Location}");
        (await response.Content.ReadAsByteArrayAsync()).Length.ShouldBeGreaterThan(0);
        if (asset.EndsWith(".mjs", StringComparison.Ordinal)) response.Content.Headers.ContentType!.MediaType!.ShouldContain("javascript");
    }

    [Fact]
    public async Task Actual_Bundles_Should_Be_Served()
    {
        var document = Parse(await GetResponseAsStringAsync("/"));
        var bundles = document.DocumentNode.SelectNodes("//script[@src]|//link[@rel='stylesheet' and @href]")!
            .Select(node => HtmlEntity.DeEntitize(node.GetAttributeValue(node.Name == "script" ? "src" : "href", "")))
            .Where(url => url.Contains("__bundles/", StringComparison.Ordinal)).Distinct().ToArray();
        bundles.Length.ShouldBeGreaterThanOrEqualTo(2);
        var hasThemeScript = false;
        foreach (var url in bundles)
        {
            var content = await GetResponseAsStringAsync(url);
            if (url.Contains(".js", StringComparison.Ordinal) && content.Contains("liquidGlassTheme", StringComparison.Ordinal)) hasThemeScript = true;
        }
        hasThemeScript.ShouldBeTrue();
    }

    [Fact]
    public async Task All_Layouts_Should_Render_Body_Sections_And_Shared_Document()
    {
        foreach (var layout in new[] { StandardLayouts.Application, StandardLayouts.Account, StandardLayouts.Empty })
        {
            var html = await RenderLayoutAsync(layout);
            html.ShouldContain("theme-test-body");
            html.ShouldContain("theme-test-style");
            html.ShouldContain("theme-test-script");
            html.ShouldContain("data-lgt-scene");
            html.ShouldContain("/Abp/ApplicationConfigurationScript");
            if (layout == StandardLayouts.Empty) html.ShouldNotContain("<header");
            else html.ShouldContain("theme-test-toolbar");
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Standard_Error_Page_Should_Be_Themed_And_Preserve_Status(HttpStatusCode status)
    {
        var html = await GetResponseAsStringAsync("/Error?httpStatusCode=" + (int)status, status);
        html.ShouldContain("lgt-error-code");
        html.ShouldContain(((int)status).ToString());
        html.ShouldContain("data-lgt-scene");
    }

    private async Task<string> RenderLayoutAsync(string layout)
    {
        using var scope = ServiceProvider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/theme-layout-test";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Theme layout test"));
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var previous = accessor.HttpContext;
        accessor.HttpContext = context;
        try
        {
            var actionContext = new ActionContext(context, new RouteData(), new ActionDescriptor());
            var path = scope.ServiceProvider.GetRequiredService<GlassTheme>().GetLayout(layout);
            var view = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null, path, true).View;
            view.ShouldNotBeNull();
            using var writer = new StringWriter();
            var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
            data["Title"] = "Theme rendering test";
            var viewContext = new ViewContext(actionContext, view, data,
                new TempDataDictionary(context, scope.ServiceProvider.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions());
            var page = (RazorPage)((RazorView)view).RazorPage;
            page.BodyContent = new Microsoft.AspNetCore.Html.HtmlString("<p id='theme-test-body'>Body</p>");
            page.PreviousSectionWriters = new Dictionary<string, RenderAsyncDelegate>
            {
                ["styles"] = () => WriteSection("<style id='theme-test-style'></style>"),
                ["scripts"] = () => WriteSection("<script id='theme-test-script'></script>"),
                ["content_toolbar"] = () => WriteSection("<button id='theme-test-toolbar'>Toolbar</button>")
            };
            await view.RenderAsync(viewContext);
            return writer.ToString();

            Task WriteSection(string content)
            {
                page.WriteLiteral(content);
                return Task.CompletedTask;
            }
        }
        finally
        {
            accessor.HttpContext = previous;
        }
    }

    private static HtmlDocument Parse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }
}
