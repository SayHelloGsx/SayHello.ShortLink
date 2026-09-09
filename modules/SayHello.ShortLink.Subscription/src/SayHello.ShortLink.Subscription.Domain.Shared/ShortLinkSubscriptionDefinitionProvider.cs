using SayHello.ShortLink.Subscription.Localization;
using SayHello.Subscription;
using SayHello.Subscription.Definitions;
using Volo.Abp.Localization;

namespace SayHello.ShortLink.Subscription;

public class ShortLinkSubscriptionDefinitionProvider : SubscriptionDefinitionProvider
{
    public override void Define(ISubscriptionDefinitionContext context)
    {
        context.AddProduct(new ProductDefinition(
            ShortLinkSubscriptionDefinitions.ProductCode,
            LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:ShortLink"),
            [
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.Statistics,
                    LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:Statistics"),
                    SubscriptionEntitlementType.Boolean),
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.MaxLinks,
                    LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:MaxLinks"),
                    SubscriptionEntitlementType.Numeric,
                    allowUnlimited: true)
            ]));
    }
}
