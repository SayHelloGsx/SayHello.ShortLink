using SayHello.ShortLink.Admin;
using SayHello.ShortLink.Public;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(ShortLinkPublicApplicationContractsModule),
    typeof(ShortLinkAdminApplicationContractsModule)
)]
public class ShortLinkApplicationContractsModule : AbpModule
{
}
