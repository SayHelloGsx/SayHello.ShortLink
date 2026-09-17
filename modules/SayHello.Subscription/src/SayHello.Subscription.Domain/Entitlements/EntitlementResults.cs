using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SayHello.Subscription.Definitions;
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

public sealed record EnumEntitlementResult
{
    public EntitlementGrantStatus Status { get; }
    public Guid? SubscriptionId { get; }
    public EntitlementSource Source { get; }
    public Guid? PlanId { get; }
    public string? Value { get; }
    public bool IsGranted => Status == EntitlementGrantStatus.Granted;

    private EnumEntitlementResult(EntitlementGrantStatus status, EntitlementSource source,
        Guid? subscriptionId, Guid? planId, string? value)
    {
        Status = status;
        Source = source;
        SubscriptionId = subscriptionId;
        PlanId = planId;
        Value = value;
    }

    public static EnumEntitlementResult NoSubscription() =>
        new(EntitlementGrantStatus.NoSubscription, EntitlementSource.None, null, null, null);

    public static EnumEntitlementResult NotGranted(Guid subscriptionId, Guid? planId = null) =>
        new(EntitlementGrantStatus.NotGranted, EntitlementSource.Subscription,
            SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), planId, null);

    public static EnumEntitlementResult FromSubscription(Guid subscriptionId, string value, Guid? planId = null) =>
        Granted(EntitlementSource.Subscription, SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), planId, value);

    public static EnumEntitlementResult NotGrantedDefaultPlan(Guid planId) =>
        new(EntitlementGrantStatus.NotGranted, EntitlementSource.DefaultPlan, null,
            SubscriptionGuard.Id(planId, nameof(planId)), null);

    public static EnumEntitlementResult FromDefaultPlan(Guid planId, string value) =>
        Granted(EntitlementSource.DefaultPlan, null, SubscriptionGuard.Id(planId, nameof(planId)), value);

    public bool Matches(string requiredValue) =>
        IsGranted &&
        string.Equals(Value, EntitlementValue.Enum(requiredValue).StringValue, StringComparison.Ordinal);

    private static EnumEntitlementResult Granted(
        EntitlementSource source, Guid? subscriptionId, Guid? planId, string value) =>
        new(EntitlementGrantStatus.Granted, source, subscriptionId, planId,
            EntitlementValue.Enum(value).StringValue);
}

public sealed record StringSetEntitlementResult
{
    public EntitlementGrantStatus Status { get; }
    public Guid? SubscriptionId { get; }
    public EntitlementSource Source { get; }
    public Guid? PlanId { get; }
    public IReadOnlyList<string> Values { get; }
    public bool IsGranted => Status == EntitlementGrantStatus.Granted;

    private StringSetEntitlementResult(EntitlementGrantStatus status, EntitlementSource source,
        Guid? subscriptionId, Guid? planId, IEnumerable<string>? values)
    {
        Status = status;
        Source = source;
        SubscriptionId = subscriptionId;
        PlanId = planId;
        Values = new ReadOnlyCollection<string>((values ?? Array.Empty<string>()).ToArray());
    }

    public static StringSetEntitlementResult NoSubscription() =>
        new(EntitlementGrantStatus.NoSubscription, EntitlementSource.None, null, null, null);

    public static StringSetEntitlementResult NotGranted(Guid subscriptionId, Guid? planId = null) =>
        new(EntitlementGrantStatus.NotGranted, EntitlementSource.Subscription,
            SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), planId, null);

    public static StringSetEntitlementResult FromSubscription(
        Guid subscriptionId, IEnumerable<string> values, Guid? planId = null) =>
        Granted(EntitlementSource.Subscription, SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId)), planId, values);

    public static StringSetEntitlementResult NotGrantedDefaultPlan(Guid planId) =>
        new(EntitlementGrantStatus.NotGranted, EntitlementSource.DefaultPlan, null,
            SubscriptionGuard.Id(planId, nameof(planId)), null);

    public static StringSetEntitlementResult FromDefaultPlan(Guid planId, IEnumerable<string> values) =>
        Granted(EntitlementSource.DefaultPlan, null, SubscriptionGuard.Id(planId, nameof(planId)), values);

    public bool Contains(string requiredValue) =>
        IsGranted && Values.Contains(EntitlementValue.Enum(requiredValue).StringValue!, StringComparer.Ordinal);

    public bool ContainsAll(IEnumerable<string> requiredValues)
    {
        var required = EntitlementValue.StringSet(requiredValues).StringValues!;
        return IsGranted && required.All(Contains);
    }

    private static StringSetEntitlementResult Granted(
        EntitlementSource source, Guid? subscriptionId, Guid? planId, IEnumerable<string> values) =>
        new(EntitlementGrantStatus.Granted, source, subscriptionId, planId,
            EntitlementValue.StringSet(values).StringValues);
}
