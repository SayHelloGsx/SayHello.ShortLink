using Volo.Abp.Modularity;

namespace SayHello.Subscription.Admin;

[DependsOn(typeof(SubscriptionCommonApplicationModule), typeof(SubscriptionAdminApplicationContractsModule))]
public class SubscriptionAdminApplicationModule : AbpModule
{
}
