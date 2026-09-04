using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.ShortLink.Admin;

[DependsOn(
    typeof(ShortLinkAdminApplicationContractsModule),
    typeof(AbpHttpClientModule)
)]
public class ShortLinkAdminHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(ShortLinkAdminApplicationContractsModule).Assembly,
            ShortLinkAdminRemoteServiceConsts.RemoteServiceName);

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ShortLinkAdminHttpApiClientModule>();
        });
    }
}
