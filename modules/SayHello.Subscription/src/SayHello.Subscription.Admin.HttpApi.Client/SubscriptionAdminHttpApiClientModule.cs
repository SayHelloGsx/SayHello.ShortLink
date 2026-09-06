using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;

namespace SayHello.Subscription.Admin;

[DependsOn(typeof(SubscriptionAdminApplicationContractsModule), typeof(AbpHttpClientModule))]
public class SubscriptionAdminHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(typeof(SubscriptionAdminApplicationContractsModule).Assembly,
            SubscriptionAdminRemoteServiceConsts.RemoteServiceName);
    }
}
