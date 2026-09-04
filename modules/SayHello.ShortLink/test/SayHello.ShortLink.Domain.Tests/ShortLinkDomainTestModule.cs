using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(ShortLinkDomainModule),
    typeof(ShortLinkTestBaseModule)
)]
public class ShortLinkDomainTestModule : AbpModule
{

}
