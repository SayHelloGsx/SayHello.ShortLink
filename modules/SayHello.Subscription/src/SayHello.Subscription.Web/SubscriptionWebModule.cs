using SayHello.Subscription.Admin.Web;
using SayHello.Subscription.Public.Web;
using Volo.Abp.Modularity;

namespace SayHello.Subscription.Web;

[DependsOn(typeof(SubscriptionPublicWebModule), typeof(SubscriptionAdminWebModule),
    typeof(SubscriptionHttpApiModule))]
public class SubscriptionWebModule : AbpModule
{
}
