using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionDomainSharedModule), typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule))]
public class SubscriptionCommonApplicationContractsModule : AbpModule
{
}
