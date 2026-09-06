using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.WebHost.EntityFrameworkCore;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.DistributedLocking;
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
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var redisConfiguration = configuration["Redis:Configuration"];
        if (string.IsNullOrWhiteSpace(redisConfiguration))
        {
            throw new AbpException("Redis:Configuration is required.");
        }

        var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
        context.Services.AddSingleton<IConnectionMultiplexer>(connectionMultiplexer);
        context.Services.AddSingleton<IDistributedLockProvider>(
            new RedisDistributedSynchronizationProvider(connectionMultiplexer.GetDatabase()));

        Configure<AbpDistributedLockOptions>(options =>
        {
            options.KeyPrefix = "SayHello.ShortLink:";
        });
    }
}
