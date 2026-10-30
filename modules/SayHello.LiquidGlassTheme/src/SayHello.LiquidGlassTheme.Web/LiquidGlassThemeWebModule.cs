using Localization.Resources.AbpUi;
using Microsoft.Extensions.DependencyInjection;
using SayHello.LiquidGlassTheme.Bundling;
using SayHello.LiquidGlassTheme.Localization;
using SayHello.LiquidGlassTheme.Toolbars;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.AspNetCore.VirtualFileSystem;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.LiquidGlassTheme;

[DependsOn(
    typeof(AbpAspNetCoreMvcUiThemeSharedModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule)
)]
public class LiquidGlassThemeWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
            builder.AddApplicationPartIfNotExists(typeof(LiquidGlassThemeWebModule).Assembly));
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<LiquidGlassThemeOptions>(
            context.Services.GetConfiguration().GetSection("LiquidGlassTheme"));

        Configure<AbpThemingOptions>(options =>
        {
            options.Themes.Add<LiquidGlassTheme>();
            options.DefaultThemeName ??= LiquidGlassTheme.Name;
        });

        Configure<AbpVirtualFileSystemOptions>(options =>
            options.FileSets.AddEmbedded<LiquidGlassThemeWebModule>());

        Configure<AbpAspNetCoreContentOptions>(options =>
        {
            options.AllowedExtraWebContentFileExtensions.Add(".mjs");
            options.ContentTypeMaps[".mjs"] = "text/javascript";
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<LiquidGlassThemeResource>("en")
                .AddBaseTypes(typeof(AbpUiResource))
                .AddVirtualJson("/Localization/LiquidGlassTheme");
        });

        Configure<AbpToolbarOptions>(options =>
            options.Contributors.Add(new LiquidGlassToolbarContributor()));

        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Add(LiquidGlassThemeBundles.Styles.Global, bundle =>
            {
                bundle.AddBaseBundles(StandardBundles.Styles.Global);
                bundle.AddContributors(typeof(LiquidGlassGlobalStyleContributor));
            });
            options.ScriptBundles.Add(LiquidGlassThemeBundles.Scripts.Global, bundle =>
            {
                bundle.AddBaseBundles(StandardBundles.Scripts.Global);
                bundle.AddContributors(typeof(LiquidGlassGlobalScriptContributor));
            });
        });

        Configure<AbpErrorPageOptions>(options =>
        {
            foreach (var statusCode in new[] { "401", "403", "404", "500" })
            {
                options.ErrorViewUrls[statusCode] = "~/Views/Error/LiquidGlass.cshtml";
            }
        });
    }
}
