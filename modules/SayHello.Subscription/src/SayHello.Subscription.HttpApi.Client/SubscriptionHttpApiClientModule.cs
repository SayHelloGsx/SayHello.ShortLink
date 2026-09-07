using SayHello.Subscription.Admin;
using SayHello.Subscription.Public;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(
    typeof(SubscriptionAdminHttpApiClientModule),
    typeof(SubscriptionPublicHttpApiClientModule),
    typeof(SubscriptionApplicationContractsModule)
)]
public class SubscriptionHttpApiClientModule : AbpModule
{
}
