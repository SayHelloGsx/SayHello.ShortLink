using System;
using SayHello.Subscription.Definitions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class SubscriptionPlanEntitlement : Entity, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid PlanId { get; private set; }
    public string FeatureKey { get; private set; } = string.Empty;
    public SubscriptionEntitlementType ValueType { get; private set; }
    public bool? BooleanValue { get; private set; }
    public long? NumericValue { get; private set; }
    public bool IsUnlimited { get; private set; }

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

    internal void SetValue(EntitlementValue value)
    {
        ValueType = value.Type;
        BooleanValue = value.BooleanValue;
        NumericValue = value.NumericValue;
        IsUnlimited = value.IsUnlimited;
    }

    public EntitlementValue ToValue() =>
        EntitlementValue.FromStorage(ValueType, BooleanValue, NumericValue, IsUnlimited);

    public override object[] GetKeys() => new object[] { PlanId, FeatureKey };
}
