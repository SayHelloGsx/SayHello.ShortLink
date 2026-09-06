using SayHello.Subscription.Admin;
using SayHello.Subscription.Public;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionPublicApplicationContractsModule), typeof(SubscriptionAdminApplicationContractsModule))]
public class SubscriptionApplicationContractsModule : AbpModule
{
}
