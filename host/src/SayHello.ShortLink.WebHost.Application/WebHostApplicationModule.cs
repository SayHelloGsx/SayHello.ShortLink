using Volo.Abp.Account;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;
using Microsoft.Extensions.DependencyInjection;

namespace SayHello.ShortLink.WebHost;

[DependsOn(
    typeof(WebHostDomainModule),
    typeof(global::SayHello.ShortLink.ShortLinkApplicationModule),
    typeof(global::SayHello.Subscription.SubscriptionApplicationModule),
    typeof(AbpAccountApplicationModule),
    typeof(WebHostApplicationContractsModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class WebHostApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<WebHostApplicationModule>();
    }
}
