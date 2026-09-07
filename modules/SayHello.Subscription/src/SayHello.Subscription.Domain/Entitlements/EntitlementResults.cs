using System;
using Volo.Abp;

namespace SayHello.Subscription.Entitlements;

public sealed record BooleanEntitlementResult
{
    public EntitlementGrantStatus Status { get; }
    public Guid? SubscriptionId { get; }
    public EntitlementSource Source { get; }
    public Guid? PlanId { get; }
    public bool IsGranted => Status == EntitlementGrantStatus.Granted;

    private BooleanEntitlementResult(EntitlementGrantStatus status, EntitlementSource source, Guid? subscriptionId, Guid? planId)
    {
        Status = status;
        SubscriptionId = subscriptionId;
        Source = source;
        PlanId = planId;
    }

    public static BooleanEntitlementResult NoSubscription() => new(EntitlementGrantStatus.NoSubscription, EntitlementSource.None, null, null);
    public static BooleanEntitlementResult FromSubscription(Guid subscriptionId, bool granted, Guid? planId = null) =>
        new(granted ? EntitlementGrantStatus.Granted : EntitlementGrantStatus.NotGranted, EntitlementSource.Subscription,
            SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), planId);
    public static BooleanEntitlementResult FromDefaultPlan(Guid planId, bool granted) =>
        new(granted ? EntitlementGrantStatus.Granted : EntitlementGrantStatus.NotGranted, EntitlementSource.DefaultPlan,
            null, SubscriptionGuard.Id(planId, nameof(planId)));
}

public sealed record NumericEntitlementResult
{
    public EntitlementGrantStatus Status { get; }
    public Guid? SubscriptionId { get; }
    public EntitlementSource Source { get; }
    public Guid? PlanId { get; }
    public long? Limit { get; }
    public bool IsUnlimited { get; }
    public bool IsGranted => Status == EntitlementGrantStatus.Granted;

    private NumericEntitlementResult(EntitlementGrantStatus status, EntitlementSource source, Guid? subscriptionId,
        Guid? planId, long? limit, bool isUnlimited)
    {
        Status = status;
        SubscriptionId = subscriptionId;
        Source = source;
        PlanId = planId;
        Limit = limit;
        IsUnlimited = isUnlimited;
    }

    public static NumericEntitlementResult NoSubscription() => new(EntitlementGrantStatus.NoSubscription, EntitlementSource.None, null, null, null, false);
    public static NumericEntitlementResult NotGranted(Guid subscriptionId, Guid? planId = null) =>
        new(EntitlementGrantStatus.NotGranted, EntitlementSource.Subscription,
            SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), planId, null, false);
    public static NumericEntitlementResult Unlimited(Guid subscriptionId, Guid? planId = null) =>
        new(EntitlementGrantStatus.Granted, EntitlementSource.Subscription,
            SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), planId, null, true);

    public static NumericEntitlementResult Finite(Guid subscriptionId, long limit, Guid? planId = null) =>
        Finite(EntitlementSource.Subscription, SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), planId, limit);

    public static NumericEntitlementResult NotGrantedDefaultPlan(Guid planId) =>
        new(EntitlementGrantStatus.NotGranted, EntitlementSource.DefaultPlan, null,
            SubscriptionGuard.Id(planId, nameof(planId)), null, false);
    public static NumericEntitlementResult UnlimitedDefaultPlan(Guid planId) =>
        new(EntitlementGrantStatus.Granted, EntitlementSource.DefaultPlan, null,
            SubscriptionGuard.Id(planId, nameof(planId)), null, true);
    public static NumericEntitlementResult FiniteDefaultPlan(Guid planId, long limit) =>
        Finite(EntitlementSource.DefaultPlan, null, SubscriptionGuard.Id(planId, nameof(planId)), limit);

    private static NumericEntitlementResult Finite(EntitlementSource source, Guid? subscriptionId, Guid? planId, long limit)
    {
        if (limit < 0)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
        }

        return new NumericEntitlementResult(EntitlementGrantStatus.Granted, source, subscriptionId, planId, limit, false);
    }

    public bool Allows(long requiredValue)
    {
        if (requiredValue < 0)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
        }

        return IsGranted && (IsUnlimited || Limit >= requiredValue);
    }
}
