using System;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;

namespace SayHello.Subscription.Entitlements;

public sealed class EffectiveEntitlementContext
{
    public EntitlementSource Source { get; }
    public UserSubscription? Subscription { get; }
    public DefaultSubscriptionPlan? DefaultPlan { get; }
    public Guid? PlanId => Subscription?.SourcePlanId ?? DefaultPlan?.Plan.Id;

    private EffectiveEntitlementContext(EntitlementSource source, UserSubscription? subscription, DefaultSubscriptionPlan? defaultPlan)
    {
        Source = source;
        Subscription = subscription;
        DefaultPlan = defaultPlan;
    }

    public static EffectiveEntitlementContext None() => new(EntitlementSource.None, null, null);
    public static EffectiveEntitlementContext FromSubscription(UserSubscription subscription) =>
        new(EntitlementSource.Subscription, subscription ?? throw new ArgumentNullException(nameof(subscription)), null);
    public static EffectiveEntitlementContext FromDefaultPlan(DefaultSubscriptionPlan plan) =>
        new(EntitlementSource.DefaultPlan, null, plan ?? throw new ArgumentNullException(nameof(plan)));
}
