using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            new FeatureDefinition("capped", new FixedLocalizableString("Capped"), SubscriptionEntitlementType.Numeric, maximum: 100),
            new FeatureDefinition("tier", new FixedLocalizableString("Tier"), SubscriptionEntitlementType.Enum),
            new FeatureDefinition("regions", new FixedLocalizableString("Regions"), SubscriptionEntitlementType.StringSet),
            new FeatureDefinition("labels", new FixedLocalizableString("Labels"), SubscriptionEntitlementType.StringSet)
        });

    public static Dictionary<string, EntitlementValue> Values(long limit = 10) => new()
    {
        ["enabled"] = EntitlementValue.Boolean(true),
        ["limit"] = EntitlementValue.Numeric(limit)
    };
}

public class SubscriptionTestEntitlementOptionProvider : ISubscriptionEntitlementOptionProvider
{
    private readonly Dictionary<string, IReadOnlyList<string>> _options = new(StringComparer.Ordinal)
    {
        ["tier"] = new[] { "starter", "pro" },
        ["regions"] = new[] { "apac", "eu", "us" }
    };

    public void SetOptions(string featureKey, IReadOnlyList<string> options) =>
        _options[featureKey] = options;

    public bool CanProvide(string productCode, string featureKey) =>
        featureKey is "tier" or "regions" or "labels";

    public Task<IReadOnlyList<string>> GetOptionsAsync(
        string productCode, string featureKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_options.TryGetValue(featureKey, out var options)
            ? options
            : (IReadOnlyList<string>)Array.Empty<string>());
}
