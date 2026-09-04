using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(ShortLinkCommonApplicationContractsModule),
    typeof(ShortLinkDomainModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpDistributedLockingModule)
)]
public class ShortLinkCommonApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<ShortLinkCommonApplicationModule>();

        var configuration = context.Services.GetConfiguration();
        Configure<ShortLinkUrlOptions>(configuration.GetSection("ShortLink:Urls"));
        Configure<ShortLinkSecurityOptions>(configuration.GetSection("ShortLink:Security"));
        Configure<ShortLinkPrivacyOptions>(configuration.GetSection("ShortLink:Privacy"));
    }

    public override async Task OnApplicationInitializationAsync(
        ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<ShortLinkMaintenanceWorker>();
    }
}
