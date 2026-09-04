using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;

namespace SayHello.ShortLink.Admin;

[DependsOn(
    typeof(ShortLinkAdminApplicationContractsModule),
    typeof(ShortLinkCommonApplicationModule),
    typeof(AbpMapperlyModule),
    typeof(AbpSettingManagementDomainModule)
)]
public class ShortLinkAdminApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<ShortLinkAdminApplicationModule>();
    }
}
