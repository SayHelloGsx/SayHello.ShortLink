using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
                         "UserAdministrationHelp", "ReplacementWarning", "FillAllExpiration" })
                localizer[key].ResourceNotFound.ShouldBeFalse(key);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
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
