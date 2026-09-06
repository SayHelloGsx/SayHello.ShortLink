using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;

namespace SayHello.Subscription;

public abstract class SubscriptionTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }
}
