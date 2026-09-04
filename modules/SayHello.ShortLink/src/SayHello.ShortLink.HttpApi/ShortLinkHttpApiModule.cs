using SayHello.ShortLink.Admin;
using SayHello.ShortLink.Public;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(ShortLinkAdminHttpApiModule),
    typeof(ShortLinkPublicHttpApiModule),
    typeof(ShortLinkApplicationContractsModule)
)]
public class ShortLinkHttpApiModule : AbpModule
{
}
