using System;
using SayHello.Subscription.Definitions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Subscriptions;

public class UserSubscriptionEntitlement : Entity, IMultiTenant
{
    public virtual Guid? TenantId { get; private set; }
    public virtual Guid SubscriptionId { get; private set; }
    public virtual string FeatureKey { get; private set; } = string.Empty;
    public virtual string DisplayName { get; private set; } = string.Empty;
    public virtual SubscriptionEntitlementType ValueType { get; private set; }
    public virtual bool? BooleanValue { get; private set; }
    public virtual long? NumericValue { get; private set; }
    public virtual bool IsUnlimited { get; private set; }
    public virtual string? StringValue { get; private set; }
    public virtual string? StringSetValue { get; private set; }

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
        StringValue = snapshot.Value.StringValue;
        StringSetValue = snapshot.Value.ToStorageStringSet();
    }

    public virtual EntitlementValue ToValue() =>
        EntitlementValue.FromStorage(ValueType, BooleanValue, NumericValue, IsUnlimited, StringValue, StringSetValue);

    public virtual EntitlementSnapshotData ToSnapshot() => new(FeatureKey, DisplayName, ToValue());

    public override object[] GetKeys() => new object[] { SubscriptionId, FeatureKey };
}
