using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace SayHello.Subscription.Admin;

[DependsOn(typeof(SubscriptionAdminApplicationContractsModule), typeof(AbpAspNetCoreMvcModule))]
public class SubscriptionAdminHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
            builder.AddApplicationPartIfNotExists(typeof(SubscriptionAdminHttpApiModule).Assembly));
    }
}
