using SayHello.ShortLink.Admin;
using SayHello.ShortLink.Public;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(ShortLinkPublicApplicationModule),
    typeof(ShortLinkAdminApplicationModule),
    typeof(ShortLinkApplicationContractsModule)
)]
public class ShortLinkApplicationModule : AbpModule
{
}
