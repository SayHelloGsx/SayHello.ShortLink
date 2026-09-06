using System;

namespace SayHello.Subscription.Subscriptions;

public sealed record SubscriptionAssignmentTarget
{
    public Guid ProductId { get; }
    public Guid PlanId { get; }
    public string ProductConcurrencyStamp { get; }
    public string PlanConcurrencyStamp { get; }
    public DateTime? ExpiresAt { get; }
    // Null explicitly asserts that no current slot exists, rather than skipping the concurrency check.
    public SubscriptionVersion? ExpectedCurrent { get; }

    public SubscriptionAssignmentTarget(Guid productId, Guid planId, string productConcurrencyStamp,
        string planConcurrencyStamp, DateTime? expiresAt, SubscriptionVersion? expectedCurrent)
    {
        ProductId = SubscriptionGuard.Id(productId, nameof(productId));
        PlanId = SubscriptionGuard.Id(planId, nameof(planId));
        ProductConcurrencyStamp = SubscriptionGuard.ConcurrencyStamp(productConcurrencyStamp);
        PlanConcurrencyStamp = SubscriptionGuard.ConcurrencyStamp(planConcurrencyStamp);
        if (expiresAt.HasValue)
        {
            SubscriptionGuard.Utc(expiresAt.Value);
        }

        ExpiresAt = expiresAt;
        ExpectedCurrent = expectedCurrent;
    }
}
