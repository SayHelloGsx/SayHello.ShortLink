using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Caching;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Public;

[DependsOn(
    typeof(ShortLinkCommonApplicationModule),
    typeof(ShortLinkPublicApplicationContractsModule),
    typeof(AbpCachingModule),
    typeof(AbpMapperlyModule)
)]
public class ShortLinkPublicApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<ShortLinkPublicApplicationModule>();
    }
}
