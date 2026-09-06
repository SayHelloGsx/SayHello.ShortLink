using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;

namespace SayHello.Subscription.Public;

[DependsOn(typeof(SubscriptionPublicApplicationContractsModule), typeof(AbpHttpClientModule))]
public class SubscriptionPublicHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(typeof(SubscriptionPublicApplicationContractsModule).Assembly,
            SubscriptionPublicRemoteServiceConsts.RemoteServiceName);
    }
}
