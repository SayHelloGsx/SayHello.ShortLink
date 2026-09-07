using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(AbpHttpClientModule),
    typeof(ShortLinkCommonApplicationContractsModule)
)]
public class ShortLinkCommonHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(ShortLinkCommonApplicationContractsModule).Assembly,
            ShortLinkCommonRemoteServiceConsts.RemoteServiceName);

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ShortLinkCommonHttpApiClientModule>();
        });
    }
}
