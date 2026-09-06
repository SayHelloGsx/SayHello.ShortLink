using Volo.Abp.Modularity;

namespace SayHello.Subscription;

public abstract class SubscriptionDomainTestBase<TStartupModule> : SubscriptionTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
}
