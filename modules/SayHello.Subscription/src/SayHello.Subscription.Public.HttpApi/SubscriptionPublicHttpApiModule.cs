using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace SayHello.Subscription.Public;

[DependsOn(
    typeof(SubscriptionPublicApplicationContractsModule),
    typeof(SubscriptionCommonHttpApiModule)
)]
public class SubscriptionPublicHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
            builder.AddApplicationPartIfNotExists(typeof(SubscriptionPublicHttpApiModule).Assembly));
    }
}
