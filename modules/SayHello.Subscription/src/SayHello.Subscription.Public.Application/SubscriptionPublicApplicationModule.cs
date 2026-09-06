using Volo.Abp.Modularity;

namespace SayHello.Subscription.Public;

[DependsOn(typeof(SubscriptionCommonApplicationModule), typeof(SubscriptionPublicApplicationContractsModule))]
public class SubscriptionPublicApplicationModule : AbpModule
{
}
