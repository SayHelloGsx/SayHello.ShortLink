using System;
using SayHello.Subscription.Definitions;

namespace SayHello.Subscription.Subscriptions;

public sealed record EntitlementSnapshotData
{
    public string FeatureKey { get; }
    public string DisplayName { get; }
    public EntitlementValue Value { get; }

    public EntitlementSnapshotData(string featureKey, string displayName, EntitlementValue value)
    {
        FeatureKey = SubscriptionCode.Normalize(featureKey, SubscriptionConsts.MaxFeatureKeyLength);
        DisplayName = SubscriptionGuard.Name(displayName);
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
