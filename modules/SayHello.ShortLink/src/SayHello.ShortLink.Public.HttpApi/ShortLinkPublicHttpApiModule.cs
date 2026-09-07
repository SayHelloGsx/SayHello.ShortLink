using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Public;

[DependsOn(
    typeof(ShortLinkPublicApplicationContractsModule),
    typeof(ShortLinkCommonHttpApiModule)
)]
public class ShortLinkPublicHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
        {
            builder.AddApplicationPartIfNotExists(typeof(ShortLinkPublicHttpApiModule).Assembly);
        });
    }
}
