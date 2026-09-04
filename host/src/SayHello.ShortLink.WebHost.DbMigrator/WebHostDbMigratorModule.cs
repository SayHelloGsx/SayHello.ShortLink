using SayHello.ShortLink.WebHost.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.WebHost.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(WebHostEntityFrameworkCoreModule),
    typeof(WebHostApplicationContractsModule)
    )]
public class WebHostDbMigratorModule : AbpModule
{
}
