using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Admin;

[DependsOn(typeof(ShortLinkCommonApplicationContractsModule))]
public class ShortLinkAdminApplicationContractsModule : AbpModule
{
}
