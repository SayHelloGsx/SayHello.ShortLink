using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp;
using Volo.Abp.Localization;

namespace SayHello.Subscription.Definitions;

public sealed class ProductDefinition
{
    public string Code { get; }
    public ILocalizableString DisplayName { get; }
    public IReadOnlyDictionary<string, FeatureDefinition> Features { get; }

    public ProductDefinition(string code, ILocalizableString displayName,
        IEnumerable<FeatureDefinition>? features = null)
    {
        Code = SubscriptionCode.Normalize(code);
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        var definitions = new Dictionary<string, FeatureDefinition>(StringComparer.Ordinal);
        foreach (var feature in features ?? Array.Empty<FeatureDefinition>())
        {
            ArgumentNullException.ThrowIfNull(feature);
            if (!definitions.TryAdd(feature.Key, feature))
            {
                throw new AbpException($"Duplicate subscription feature definition: {Code}/{feature.Key}.");
            }
        }

        Features = new ReadOnlyDictionary<string, FeatureDefinition>(definitions);
    }

    public FeatureDefinition GetFeature(string key)
    {
        var normalized = SubscriptionCode.Normalize(key, SubscriptionConsts.MaxFeatureKeyLength);
        return Features.TryGetValue(normalized, out var feature)
            ? feature
            : throw new BusinessException(SubscriptionErrorCodes.UnknownFeature)
                .WithData("ProductCode", Code).WithData("FeatureKey", normalized);
    }
}
