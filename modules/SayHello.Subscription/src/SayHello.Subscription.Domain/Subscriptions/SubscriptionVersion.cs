using System;

namespace SayHello.Subscription.Subscriptions;

public sealed record SubscriptionVersion
{
    public Guid SubscriptionId { get; }
    public string ConcurrencyStamp { get; }

    public SubscriptionVersion(Guid subscriptionId, string concurrencyStamp)
    {
        SubscriptionId = SubscriptionGuard.Id(subscriptionId, nameof(subscriptionId));
        ConcurrencyStamp = SubscriptionGuard.ConcurrencyStamp(concurrencyStamp);
    }
}
