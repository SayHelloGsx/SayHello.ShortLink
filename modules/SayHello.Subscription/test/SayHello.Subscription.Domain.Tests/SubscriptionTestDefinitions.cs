using System;
using System.Collections.Generic;
using SayHello.Subscription.Definitions;
using Volo.Abp.Localization;

namespace SayHello.Subscription;

public class SubscriptionTestDefinitions : SubscriptionDefinitionProvider
{
    public override void Define(ISubscriptionDefinitionContext context)
    {
        foreach (var code in new[] { "alpha", "beta", "gamma" })
            context.AddProduct(Product(code));
    }

    public static ProductDefinition Product(string code) =>
        new(code, new FixedLocalizableString(code), new[]
        {
            new FeatureDefinition("enabled", new FixedLocalizableString("Enabled"), SubscriptionEntitlementType.Boolean),
            new FeatureDefinition("limit", new FixedLocalizableString("Limit"), SubscriptionEntitlementType.Numeric, allowUnlimited: true),
            new FeatureDefinition("future", new FixedLocalizableString("New feature"), SubscriptionEntitlementType.Boolean),
            new FeatureDefinition("missing-limit", new FixedLocalizableString("Missing limit"), SubscriptionEntitlementType.Numeric),
            new FeatureDefinition("capped", new FixedLocalizableString("Capped"), SubscriptionEntitlementType.Numeric, maximum: 100)
        });

    public static Dictionary<string, EntitlementValue> Values(long limit = 10) => new()
    {
        ["enabled"] = EntitlementValue.Boolean(true),
        ["limit"] = EntitlementValue.Numeric(limit)
    };
}
