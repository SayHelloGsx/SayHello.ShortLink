using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SayHello.Subscription.Public.Localization;
using SayHello.Subscription.Public.Web.Menus;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.Subscription.Public.Web;

[DependsOn(typeof(SubscriptionPublicApplicationContractsModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule), typeof(AbpMapperlyModule))]
public class SubscriptionPublicWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
            options.AddAssemblyResource(typeof(SubscriptionPublicResource), typeof(SubscriptionPublicWebModule).Assembly));
        PreConfigure<IMvcBuilder>(builder =>
            builder.AddApplicationPartIfNotExists(typeof(SubscriptionPublicWebModule).Assembly));
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
            options.FileSets.AddEmbedded<SubscriptionPublicWebModule>());
        context.Services.AddMapperlyObjectMapper<SubscriptionPublicWebModule>();
        Configure<AbpNavigationOptions>(options =>
            options.MenuContributors.Add(new SubscriptionPublicMenuContributor()));
        Configure<RazorPagesOptions>(options =>
            options.Conventions.AuthorizePage("/Public/Subscriptions/Mine"));
    }
}
