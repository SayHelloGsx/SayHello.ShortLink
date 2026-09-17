using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.Subscription.Subscriptions;

/// <summary>
/// Treats UserId as an opaque external reference whose existence is validated by the calling application.
/// Validates the current tenant and non-empty user identity. Mutations hold the tenant/user lock through
/// transactional unit-of-work completion. Bundle targets must exactly match the previewed bundle; check
/// bundle, product, plan and expected-current versions before retiring only the targeted slots. Every
/// assignment creates new independent product rows and immutable snapshots, including reassignment.
/// </summary>
public interface ISubscriptionManager
{
    Task<SubscriptionAssignmentPreview> PreviewPlanAsync(Guid userId, Guid planId,
        CancellationToken cancellationToken = default);
    Task<SubscriptionAssignmentPreview> PreviewBundleAsync(Guid userId, Guid bundleId,
        CancellationToken cancellationToken = default);
    Task<UserSubscription> AssignPlanAsync(AssignSubscriptionPlan input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscription>> AssignBundleAsync(AssignSubscriptionBundle input,
        CancellationToken cancellationToken = default);
    Task<UserSubscription> RevokeAsync(Guid subscriptionId, string concurrencyStamp,
        string? reason = null, CancellationToken cancellationToken = default);
    Task<UserSubscription> AdjustExpirationAsync(Guid subscriptionId, string concurrencyStamp,
        DateTime? expiresAt, CancellationToken cancellationToken = default);
}
