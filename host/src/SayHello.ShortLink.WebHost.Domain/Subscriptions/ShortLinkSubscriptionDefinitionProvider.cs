using SayHello.ShortLink.WebHost.Localization;
using SayHello.Subscription;
using SayHello.Subscription.Definitions;
using Volo.Abp.Localization;

namespace SayHello.ShortLink.WebHost.Subscriptions;

public class ShortLinkSubscriptionDefinitionProvider : SubscriptionDefinitionProvider
{
    public override void Define(ISubscriptionDefinitionContext context)
    {
        context.AddProduct(new ProductDefinition(
            ShortLinkSubscriptionDefinitions.ProductCode,
            LocalizableString.Create<WebHostResource>("Subscription:ShortLink"),
            [
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.Statistics,
                    LocalizableString.Create<WebHostResource>("Subscription:Statistics"),
                    SubscriptionEntitlementType.Boolean),
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.MaxLinks,
                    LocalizableString.Create<WebHostResource>("Subscription:MaxLinks"),
                    SubscriptionEntitlementType.Numeric,
                    allowUnlimited: true)
            ]));
    }
}
