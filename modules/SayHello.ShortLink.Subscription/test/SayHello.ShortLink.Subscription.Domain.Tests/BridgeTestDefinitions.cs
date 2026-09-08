using SayHello.Subscription;
using SayHello.Subscription.Definitions;
using Volo.Abp.Localization;

namespace SayHello.ShortLink.Subscription;

public sealed class BridgeTestDefinitions : SubscriptionDefinitionProvider
{
    public const string ProductCode = "test-product";
    public const string QuotaFeatureKey = "capacity";
    public const string StatisticsFeatureKey = "reports";

    public static ShortLinkSubscriptionOptions Options() => new()
    {
        ProductCode = ProductCode,
        QuotaFeatureKey = QuotaFeatureKey,
        StatisticsFeatureKey = StatisticsFeatureKey
    };

    public static ProductDefinition Product() => new(
        ProductCode,
        new FixedLocalizableString("Test product"),
        [
            new FeatureDefinition(QuotaFeatureKey, new FixedLocalizableString("Capacity"),
                SubscriptionEntitlementType.Numeric, allowUnlimited: true),
            new FeatureDefinition(StatisticsFeatureKey, new FixedLocalizableString("Reports"),
                SubscriptionEntitlementType.Boolean)
        ]);

    public override void Define(ISubscriptionDefinitionContext context) => context.AddProduct(Product());
}
