using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SayHello.Subscription.Definitions;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(typeof(SubscriptionDomainModule), typeof(SubscriptionTestBaseModule))]
public class SubscriptionDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<SubscriptionDefinitionOptions>(options => options.DefinitionProviders.Add<SubscriptionTestDefinitions>());
        context.Services.AddSingleton<SubscriptionTestEntitlementOptionProvider>();
        context.Services.Replace(ServiceDescriptor.Singleton<ISubscriptionEntitlementOptionProvider>(provider =>
            provider.GetRequiredService<SubscriptionTestEntitlementOptionProvider>()));
    }
}
