using SayHello.ShortLink.Public;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(ShortLinkPublicApplicationModule),
    typeof(ShortLinkDomainTestModule)
    )]
public class ShortLinkApplicationTestModule : AbpModule
{

}
