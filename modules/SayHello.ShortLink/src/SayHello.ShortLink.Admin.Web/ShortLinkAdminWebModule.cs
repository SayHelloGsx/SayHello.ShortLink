using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Admin.Web.Menus;
using SayHello.ShortLink.Localization;
using SayHello.ShortLink.Permissions;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.ShortLink.Admin.Web;

[DependsOn(
    typeof(ShortLinkAdminApplicationContractsModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule),
    typeof(AbpMapperlyModule)
)]
public class ShortLinkAdminWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(ShortLinkResource),
                typeof(ShortLinkAdminWebModule).Assembly);
        });

        PreConfigure<IMvcBuilder>(builder =>
        {
            builder.AddApplicationPartIfNotExists(typeof(ShortLinkAdminWebModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new ShortLinkAdminMenuContributor());
        });

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ShortLinkAdminWebModule>();
        });

        context.Services.AddMapperlyObjectMapper<ShortLinkAdminWebModule>();

        Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AuthorizeFolder(
                "/Admin/ShortLinks",
                ShortLinkAdminPermissions.Default);
        });
    }
}
