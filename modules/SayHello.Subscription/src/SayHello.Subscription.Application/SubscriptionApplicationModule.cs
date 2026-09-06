using SayHello.Subscription.Admin;
using SayHello.Subscription.Public;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionApplicationContractsModule),
    typeof(SubscriptionPublicApplicationModule), typeof(SubscriptionAdminApplicationModule))]
public class SubscriptionApplicationModule : AbpModule
{
}
