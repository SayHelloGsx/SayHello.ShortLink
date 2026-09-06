using Volo.Abp.Collections;

namespace SayHello.Subscription.Definitions;

public class SubscriptionDefinitionOptions
{
    public ITypeList<ISubscriptionDefinitionProvider> DefinitionProviders { get; } =
        new TypeList<ISubscriptionDefinitionProvider>();
}
