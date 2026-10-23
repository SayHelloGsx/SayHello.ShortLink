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
                    SubscriptionEntitlementType.Enum),
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.Domains,
                    LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:Domains"),
                    SubscriptionEntitlementType.StringSet),
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.MaxLinks,
                    LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:MaxLinks"),
                    SubscriptionEntitlementType.Numeric,
                    allowUnlimited: true),
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.MaxCustomCodes,
                    LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:MaxCustomCodes"),
                    SubscriptionEntitlementType.Numeric,
                    allowUnlimited: true),
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.Namespaces,
                    LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:Namespaces"),
                    SubscriptionEntitlementType.Boolean),
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.MaxNamespaces,
                    LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:MaxNamespaces"),
                    SubscriptionEntitlementType.Numeric,
                    allowUnlimited: true),
                new FeatureDefinition(
                    ShortLinkSubscriptionDefinitions.MaxRootLinks,
                    LocalizableString.Create<ShortLinkSubscriptionResource>("Subscription:MaxRootLinks"),
                    SubscriptionEntitlementType.Numeric,
                    allowUnlimited: true)
            ]));
    }
}
