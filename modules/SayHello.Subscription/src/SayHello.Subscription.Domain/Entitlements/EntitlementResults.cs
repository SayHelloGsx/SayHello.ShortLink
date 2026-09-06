using System;
using Volo.Abp;

namespace SayHello.Subscription.Entitlements;

public sealed record BooleanEntitlementResult
{
    public EntitlementGrantStatus Status { get; }
    public Guid? SubscriptionId { get; }
    public bool IsGranted => Status == EntitlementGrantStatus.Granted;

    private BooleanEntitlementResult(EntitlementGrantStatus status, Guid? subscriptionId)
    {
        Status = status;
        SubscriptionId = subscriptionId;
    }

    public static BooleanEntitlementResult NoSubscription() => new(EntitlementGrantStatus.NoSubscription, null);
    public static BooleanEntitlementResult FromSubscription(Guid subscriptionId, bool granted) =>
        new(granted ? EntitlementGrantStatus.Granted : EntitlementGrantStatus.NotGranted,
            SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)));
}

public sealed record NumericEntitlementResult
{
    public EntitlementGrantStatus Status { get; }
    public Guid? SubscriptionId { get; }
    public long? Limit { get; }
    public bool IsUnlimited { get; }
    public bool IsGranted => Status == EntitlementGrantStatus.Granted;

    private NumericEntitlementResult(EntitlementGrantStatus status, Guid? subscriptionId, long? limit, bool isUnlimited)
    {
        Status = status;
        SubscriptionId = subscriptionId;
        Limit = limit;
        IsUnlimited = isUnlimited;
    }

    public static NumericEntitlementResult NoSubscription() => new(EntitlementGrantStatus.NoSubscription, null, null, false);
    public static NumericEntitlementResult NotGranted(Guid subscriptionId) =>
        new(EntitlementGrantStatus.NotGranted, SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), null, false);
    public static NumericEntitlementResult Unlimited(Guid subscriptionId) =>
        new(EntitlementGrantStatus.Granted, SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), null, true);

    public static NumericEntitlementResult Finite(Guid subscriptionId, long limit)
    {
        if (limit < 0)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
        }

        return new NumericEntitlementResult(EntitlementGrantStatus.Granted,
            SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), limit, false);
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
