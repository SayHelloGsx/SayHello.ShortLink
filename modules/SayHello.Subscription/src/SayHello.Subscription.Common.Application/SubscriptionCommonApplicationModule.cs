using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionDomainModule), typeof(SubscriptionCommonApplicationContractsModule),
    typeof(AbpDddApplicationModule), typeof(AbpMapperlyModule))]
public class SubscriptionCommonApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SubscriptionCommonApplicationModule>();
    }
}
