using SayHello.ShortLink.Admin;
using SayHello.ShortLink.Public;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(ShortLinkAdminHttpApiClientModule),
    typeof(ShortLinkPublicHttpApiClientModule),
    typeof(ShortLinkApplicationContractsModule)
)]
public class ShortLinkHttpApiClientModule : AbpModule
{
}
