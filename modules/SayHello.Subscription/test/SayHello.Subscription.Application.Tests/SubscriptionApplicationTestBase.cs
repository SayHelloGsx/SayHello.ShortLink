using Volo.Abp.Modularity;

namespace SayHello.Subscription;

public abstract class SubscriptionApplicationTestBase<TStartupModule> : SubscriptionDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
}
