using System;
using SayHello.Subscription.Definitions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Subscriptions;

public class UserSubscriptionEntitlement : Entity, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public string FeatureKey { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public SubscriptionEntitlementType ValueType { get; private set; }
    public bool? BooleanValue { get; private set; }
    public long? NumericValue { get; private set; }
    public bool IsUnlimited { get; private set; }

    protected UserSubscriptionEntitlement()
    {
    }

    internal UserSubscriptionEntitlement(Guid? tenantId, Guid subscriptionId, EntitlementSnapshotData snapshot)
    {
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        FeatureKey = snapshot.FeatureKey;
        DisplayName = snapshot.DisplayName;
        ValueType = snapshot.Value.Type;
        BooleanValue = snapshot.Value.BooleanValue;
        NumericValue = snapshot.Value.NumericValue;
        IsUnlimited = snapshot.Value.IsUnlimited;
    }

    public EntitlementValue ToValue() =>
        EntitlementValue.FromStorage(ValueType, BooleanValue, NumericValue, IsUnlimited);

    public EntitlementSnapshotData ToSnapshot() => new(FeatureKey, DisplayName, ToValue());

    public override object[] GetKeys() => new object[] { SubscriptionId, FeatureKey };
}
