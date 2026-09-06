using Microsoft.Extensions.DependencyInjection;
using SayHello.Subscription.Admin.Localization;
using SayHello.Subscription.Admin.Web.Menus;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.UI.Navigation;

namespace SayHello.Subscription.Admin.Web;

[DependsOn(typeof(SubscriptionAdminApplicationContractsModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule), typeof(AbpMapperlyModule))]
public class SubscriptionAdminWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
            options.AddAssemblyResource(typeof(SubscriptionAdminResource), typeof(SubscriptionAdminWebModule).Assembly));
        PreConfigure<IMvcBuilder>(builder =>
            builder.AddApplicationPartIfNotExists(typeof(SubscriptionAdminWebModule).Assembly));
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
            options.FileSets.AddEmbedded<SubscriptionAdminWebModule>());
        context.Services.AddMapperlyObjectMapper<SubscriptionAdminWebModule>();
        Configure<AbpNavigationOptions>(options => options.MenuContributors.Add(new SubscriptionAdminMenuContributor()));
    }
}
