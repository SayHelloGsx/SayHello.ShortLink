using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Public;

[DependsOn(typeof(ShortLinkCommonApplicationContractsModule))]
public class ShortLinkPublicApplicationContractsModule : AbpModule
{
}
