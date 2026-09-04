using SayHello.ShortLink.Admin.Web;
using SayHello.ShortLink.Public.Web;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Web;

[DependsOn(
    typeof(ShortLinkPublicWebModule),
    typeof(ShortLinkAdminWebModule),
    typeof(ShortLinkApplicationContractsModule)
)]
public class ShortLinkWebModule : AbpModule
{
}
