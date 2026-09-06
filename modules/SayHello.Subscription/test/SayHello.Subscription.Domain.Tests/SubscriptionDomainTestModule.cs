using SayHello.Subscription.Definitions;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionDomainModule), typeof(SubscriptionTestBaseModule))]
public class SubscriptionDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<SubscriptionDefinitionOptions>(options => options.DefinitionProviders.Add<SubscriptionTestDefinitions>());
    }
}
