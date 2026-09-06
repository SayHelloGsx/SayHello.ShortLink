using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionApplicationModule), typeof(SubscriptionDomainTestModule))]
public class SubscriptionApplicationTestModule : AbpModule
{
}
