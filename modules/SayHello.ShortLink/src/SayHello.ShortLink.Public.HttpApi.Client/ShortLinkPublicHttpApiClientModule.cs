using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.ShortLink.Public;

[DependsOn(
    typeof(ShortLinkPublicApplicationContractsModule),
    typeof(AbpHttpClientModule)
)]
public class ShortLinkPublicHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(ShortLinkPublicApplicationContractsModule).Assembly,
            ShortLinkPublicRemoteServiceConsts.RemoteServiceName);

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ShortLinkPublicHttpApiClientModule>();
        });
    }
}
