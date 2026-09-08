using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Subscription;

[DependsOn(
    typeof(ShortLinkDomainModule),
    typeof(global::SayHello.Subscription.SubscriptionDomainModule))]
public class ShortLinkSubscriptionDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<ShortLinkSubscriptionOptions>();
        context.Services.AddSingleton<IValidateOptions<ShortLinkSubscriptionOptions>, ShortLinkSubscriptionOptionsValidator>();
    }

    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.RemoveAll<IShortLinkCapabilityProvider>();
        context.Services.AddTransient<IShortLinkCapabilityProvider, SubscriptionShortLinkCapabilityProvider>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // The host's definition providers and options are complete only after every module is configured.
        _ = context.ServiceProvider.GetRequiredService<IOptions<ShortLinkSubscriptionOptions>>().Value;
    }
}
