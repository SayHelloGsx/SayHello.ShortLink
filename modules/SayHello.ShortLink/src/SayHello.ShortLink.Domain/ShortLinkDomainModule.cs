using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(ShortLinkDomainSharedModule)
)]
public class ShortLinkDomainModule : AbpModule
{

}
