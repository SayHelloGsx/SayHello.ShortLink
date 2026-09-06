using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(AbpTestBaseModule), typeof(AbpAutofacModule),
    typeof(AbpAuthorizationModule), typeof(AbpGuidsModule))]
public class SubscriptionTestBaseModule : AbpModule
{
}
