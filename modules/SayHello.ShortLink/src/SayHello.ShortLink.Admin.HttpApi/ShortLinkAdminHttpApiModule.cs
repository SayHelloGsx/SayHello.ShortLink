using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Admin;

[DependsOn(
    typeof(ShortLinkAdminApplicationContractsModule),
    typeof(ShortLinkCommonHttpApiModule)
)]
public class ShortLinkAdminHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
        {
            builder.AddApplicationPartIfNotExists(typeof(ShortLinkAdminHttpApiModule).Assembly);
        });
    }
}
