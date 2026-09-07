using SayHello.Subscription.Admin;
using SayHello.Subscription.Public;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(
    typeof(SubscriptionAdminHttpApiModule),
    typeof(SubscriptionPublicHttpApiModule),
    typeof(SubscriptionApplicationContractsModule)
)]
public class SubscriptionHttpApiModule : AbpModule
{
}
