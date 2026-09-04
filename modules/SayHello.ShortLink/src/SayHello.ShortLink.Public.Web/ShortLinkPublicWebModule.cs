using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Localization;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.Web.Menus;
using SayHello.ShortLink.Public.Web.Routing;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.ShortLink.Public.Web;

[DependsOn(
    typeof(ShortLinkPublicApplicationContractsModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule),
    typeof(AbpMapperlyModule)
)]
public class ShortLinkPublicWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(ShortLinkResource),
                typeof(ShortLinkPublicWebModule).Assembly);
        });

        PreConfigure<IMvcBuilder>(builder =>
        {
            builder.AddApplicationPartIfNotExists(typeof(ShortLinkPublicWebModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new ShortLinkPublicMenuContributor());
        });

        Configure<RouteOptions>(options =>
        {
            options.ConstraintMap["shortCode"] = typeof(ShortCodeRouteConstraint);
        });

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ShortLinkPublicWebModule>();
        });

        context.Services.AddMapperlyObjectMapper<ShortLinkPublicWebModule>();

        Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AuthorizeFolder(
                "/Public/ShortLinks",
                ShortLinkPublicPermissions.Default);
        });
    }
}
