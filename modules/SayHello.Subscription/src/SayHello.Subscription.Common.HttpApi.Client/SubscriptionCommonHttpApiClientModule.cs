using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.Subscription;

[DependsOn(
    typeof(AbpHttpClientModule),
    typeof(SubscriptionCommonApplicationContractsModule)
)]
public class SubscriptionCommonHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(SubscriptionCommonApplicationContractsModule).Assembly,
            SubscriptionCommonRemoteServiceConsts.RemoteServiceName);

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SubscriptionCommonHttpApiClientModule>();
        });
    }
}
