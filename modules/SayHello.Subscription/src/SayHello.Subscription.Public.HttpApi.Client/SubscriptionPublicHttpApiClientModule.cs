using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace SayHello.Subscription.Public;

[DependsOn(
    typeof(SubscriptionPublicApplicationContractsModule),
    typeof(SubscriptionCommonHttpApiClientModule)
)]
public class SubscriptionPublicHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(typeof(SubscriptionPublicApplicationContractsModule).Assembly,
            SubscriptionPublicRemoteServiceConsts.RemoteServiceName);
    }
}
