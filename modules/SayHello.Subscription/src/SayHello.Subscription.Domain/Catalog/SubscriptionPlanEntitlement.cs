using System;
using SayHello.Subscription.Definitions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class SubscriptionPlanEntitlement : Entity, IMultiTenant
{
    public virtual Guid? TenantId { get; private set; }
    public virtual Guid PlanId { get; private set; }
    public virtual string FeatureKey { get; private set; } = string.Empty;
    public virtual SubscriptionEntitlementType ValueType { get; private set; }
    public virtual bool? BooleanValue { get; private set; }
    public virtual long? NumericValue { get; private set; }
    public virtual bool IsUnlimited { get; private set; }
    public virtual string? StringValue { get; private set; }
    public virtual string? StringSetValue { get; private set; }

    protected SubscriptionPlanEntitlement()
    {
    }

    internal SubscriptionPlanEntitlement(Guid? tenantId, Guid planId, string featureKey, EntitlementValue value)
    {
        TenantId = tenantId;
        PlanId = planId;
        FeatureKey = featureKey;
        SetValue(value);
    }

    internal virtual void SetValue(EntitlementValue value)
    {
        ValueType = value.Type;
        BooleanValue = value.BooleanValue;
        NumericValue = value.NumericValue;
        IsUnlimited = value.IsUnlimited;
        StringValue = value.StringValue;
        StringSetValue = value.ToStorageStringSet();
    }

    public virtual EntitlementValue ToValue() =>
        EntitlementValue.FromStorage(ValueType, BooleanValue, NumericValue, IsUnlimited, StringValue, StringSetValue);

    public override object[] GetKeys() => new object[] { PlanId, FeatureKey };
}
