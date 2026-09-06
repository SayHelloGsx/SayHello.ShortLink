using Volo.Abp.Domain;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(AbpDddDomainModule), typeof(SubscriptionDomainSharedModule), typeof(AbpDistributedLockingModule))]
public class SubscriptionDomainModule : AbpModule
{
}
