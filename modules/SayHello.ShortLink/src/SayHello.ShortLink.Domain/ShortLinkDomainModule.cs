using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using Volo.Abp.DistributedLocking;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpDistributedLockingModule),
    typeof(ShortLinkDomainSharedModule)
)]
public class ShortLinkDomainModule : AbpModule
{

}
