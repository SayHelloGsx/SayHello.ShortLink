using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using NUglify;
using NUglify.JavaScript;
using SayHello.Subscription.Admin.Localization;
using Shouldly;
using Volo.Abp.VirtualFileSystem;
using Xunit;

namespace SayHello.Subscription.AdminTests;

public class AdminPresentationTests : SubscriptionTestBase<AdminSurfaceTestModule>
{
    [Theory]
    [InlineData("en", "Publication state", "Page size", "Published")]
    [InlineData("zh-Hans", "发布状态", "每页条数", "已发布")]
    public void Admin_localization_is_resolved_through_virtual_files_and_localizers(
        string culture, string state, string pageSize, string published)
    {
        var provider = GetRequiredService<IVirtualFileProvider>();
        provider.GetFileInfo($"/Localization/SubscriptionAdmin/{culture}.json").Exists.ShouldBeTrue();
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var localizer = GetRequiredService<IStringLocalizer<SubscriptionAdminResource>>();
            localizer["State"].Value.ShouldBe(state);
            localizer["PageSize"].Value.ShouldBe(pageSize);
            localizer["State:1"].Value.ShouldBe(published);
            foreach (var key in new[] { "CatalogHelp", "DisplayOrder", "BundleCompositionHelp",
                         "UserAdministrationHelp", "ReplacementWarning", "FillAllExpiration",
                         "DefaultPlan", "DefaultPlanId", "ConfigureDefaultPlan", "ClearDefaultPlan",
                         "DefaultPlanUnconfigured", "DefaultPlanHelp", "DefaultPlanRightsWarning",
                         "DefaultPlanPublishedOnly", "ConfirmDefaultPlan", "ConfirmClearDefaultPlan",
                         "DefaultPlanSaveFailed", "Select a valid default plan or explicitly clear it.",
                         SubscriptionErrorCodes.InvalidDefaultPlan, SubscriptionErrorCodes.DefaultPlanInUse })
                localizer[key].ResourceNotFound.ShouldBeFalse(key);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void Default_configuration_uses_permission_gated_separate_form_paged_picker_and_explicit_confirmation()
    {
        var directory = Path.Combine(FindRepository(), "modules", "SayHello.Subscription", "src",
            "SayHello.Subscription.Admin.Web", "Pages", "Admin", "Subscriptions");
        var markup = File.ReadAllText(Path.Combine(directory, "_Catalog.cshtml"));
        markup.ShouldContain("""Model.AreaName == "Products" && Model.CanUpdate""");
        markup.ShouldContain("id=\"default-plan-form\" method=\"post\"");
        markup.ShouldContain("id=\"clear-default-plan\" type=\"button\"");
        markup.ShouldContain("""@L["DefaultPlanRightsWarning"]""");
        var defaultForm = markup[markup.IndexOf("id=\"default-plan-form\"", StringComparison.Ordinal)..];
        defaultForm[..defaultForm.IndexOf("</form>", StringComparison.Ordinal)].ShouldContain("@Html.AntiForgeryToken()");

        var script = File.ReadAllText(Path.Combine(directory, "Catalog.js"));
        script.ShouldContain("function defaultStatus(item)");
        script.ShouldContain("item.defaultPlanName || item.defaultPlanId");
        script.ShouldContain("l('DefaultPlanUnconfigured')");
        script.ShouldContain("s.picker($('#default-plan-picker'), 'DefaultPlanOptions'");
        script.ShouldContain("{ id: item.id }");
        script.ShouldContain("l('ConfirmClearDefaultPlan', item.name)");
        script.ShouldContain("l('ConfirmDefaultPlan', plan.name, item.name)");
        script.ShouldContain("l('DefaultPlanRightsWarning')");
        script.ShouldContain("concurrencyStamp: item.concurrencyStamp, planId: clear ? null : plan.id");
        script.ShouldContain("$('#default-plan-validation').text(details.message");

        var handlers = File.ReadAllText(Path.Combine(directory, "Products.cshtml.cs"));
        handlers.ShouldContain("WriteAsync(SubscriptionAdminPermissions.Products.Update, () => _service.SetDefaultPlanAsync(id, input))");
    }

    [Fact]
    public void English_and_Chinese_admin_resources_have_matching_keys_and_default_warnings()
    {
        var provider = GetRequiredService<IVirtualFileProvider>();
        using var enStream = provider.GetFileInfo("/Localization/SubscriptionAdmin/en.json").CreateReadStream();
        using var zhStream = provider.GetFileInfo("/Localization/SubscriptionAdmin/zh-Hans.json").CreateReadStream();
        using var en = JsonDocument.Parse(enStream);
        using var zh = JsonDocument.Parse(zhStream);
        var english = en.RootElement.GetProperty("texts");
        var chinese = zh.RootElement.GetProperty("texts");
        english.EnumerateObject().Select(property => property.Name).OrderBy(name => name)
            .ShouldBe(chinese.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        english.GetProperty("DefaultPlan").GetString().ShouldBe("Default Free plan");
        chinese.GetProperty("DefaultPlan").GetString().ShouldBe("默认免费套餐");
        english.GetProperty("ConfirmClearDefaultPlan").GetString()!.ShouldContain("lose fallback rights");
        chinese.GetProperty("ConfirmClearDefaultPlan").GetString()!.ShouldContain("失去兜底权益");
        english.GetProperty("DefaultPlanRightsWarning").GetString()!.ShouldContain("immediately");
        chinese.GetProperty("DefaultPlanRightsWarning").GetString()!.ShouldContain("立即");
    }

    [Theory]
    [InlineData("Shared.js")]
    [InlineData("Catalog.js")]
    [InlineData("Users.js")]
    public void Production_minification_does_not_move_functions_outside_their_lexical_variables(string file)
    {
        var source = File.ReadAllText(Path.Combine(FindRepository(), "modules", "SayHello.Subscription", "src",
            "SayHello.Subscription.Admin.Web", "Pages", "Admin", "Subscriptions", file));
        var minified = Uglify.Js(source);
        minified.HasErrors.ShouldBeFalse(string.Join(Environment.NewLine, minified.Errors));

        // Reparse the output: NUglify can incorrectly turn an early return into a block containing
        // let/const declarations while hoisting their referencing functions outside that block.
        var undefined = new List<string>();
        var parser = new JSParser();
        parser.UndefinedReference += (_, args) => undefined.Add(args.Reference.Name);
        var settings = new CodeSettings();
        settings.SetKnownGlobalIdentifiers(new[] { "$", "abp", "window", "location", "BigInt" });
        parser.Parse(minified.Code, settings);
        undefined.ShouldBeEmpty();
    }

    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "modules", "SayHello.Subscription", "src")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("The subscription module source directory was not found.");
    }
}
