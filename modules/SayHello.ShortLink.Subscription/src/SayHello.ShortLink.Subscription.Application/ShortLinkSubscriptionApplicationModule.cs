using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SayHello.ShortLink.ShortLinks;
using SayHello.Subscription.Public;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Subscription;

[DependsOn(
    typeof(ShortLinkDomainModule),
    typeof(ShortLinkSubscriptionDomainSharedModule),
    typeof(SubscriptionPublicApplicationContractsModule))]
public class ShortLinkSubscriptionApplicationModule : AbpModule
{
    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.RemoveAll<IShortLinkCapabilityProvider>();
        context.Services.AddTransient<IShortLinkCapabilityProvider, SubscriptionShortLinkCapabilityProvider>();
    }
}
