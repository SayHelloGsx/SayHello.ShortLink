using Volo.Abp.DependencyInjection;

namespace SayHello.Subscription.Definitions;

public interface ISubscriptionDefinitionProvider
{
    void Define(ISubscriptionDefinitionContext context);
}

public interface ISubscriptionDefinitionContext
{
    void AddProduct(ProductDefinition product);
}

public abstract class SubscriptionDefinitionProvider : ISubscriptionDefinitionProvider, ITransientDependency
{
    public abstract void Define(ISubscriptionDefinitionContext context);
}
