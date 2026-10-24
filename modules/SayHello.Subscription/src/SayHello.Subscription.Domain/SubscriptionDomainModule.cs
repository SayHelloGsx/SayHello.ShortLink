using Volo.Abp.Domain;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Modularity;
using Volo.Abp.Users;

namespace SayHello.Subscription;

[DependsOn(typeof(AbpDddDomainModule), typeof(SubscriptionDomainSharedModule),
    typeof(AbpDistributedLockingAbstractionsModule), typeof(AbpUsersDomainModule))]
public class SubscriptionDomainModule : AbpModule
{
}
