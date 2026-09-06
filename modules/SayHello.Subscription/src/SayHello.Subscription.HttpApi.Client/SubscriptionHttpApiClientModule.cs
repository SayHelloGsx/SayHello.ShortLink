using SayHello.Subscription.Admin;
using SayHello.Subscription.Public;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionPublicHttpApiClientModule), typeof(SubscriptionAdminHttpApiClientModule))]
public class SubscriptionHttpApiClientModule : AbpModule
{
}
