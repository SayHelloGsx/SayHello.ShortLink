using SayHello.Subscription.Admin;
using SayHello.Subscription.Public;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionPublicHttpApiModule), typeof(SubscriptionAdminHttpApiModule))]
public class SubscriptionHttpApiModule : AbpModule
{
}
