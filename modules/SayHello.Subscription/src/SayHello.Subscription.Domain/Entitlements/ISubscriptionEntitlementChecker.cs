using System;
using System.Threading;
using System.Threading.Tasks;
using SayHello.Subscription.Subscriptions;

namespace SayHello.Subscription.Entitlements;

/// <summary>
/// Uses the ABP clock and stored snapshots, not today's plan values. Unknown keys and type mismatches
/// are errors even without a subscription. Require methods throw for missing subscriptions or grants;
/// RequireNumericAsync compares a non-negative required value, including zero, without consuming quota.
/// </summary>
public interface ISubscriptionEntitlementChecker
{
    Task<UserSubscription?> FindEffectiveSubscriptionAsync(Guid? tenantId, Guid userId, string productCode,
        CancellationToken cancellationToken = default);
    Task<BooleanEntitlementResult> GetBooleanAsync(Guid? tenantId, Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default);
    Task RequireBooleanAsync(Guid? tenantId, Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default);
    Task<NumericEntitlementResult> GetNumericAsync(Guid? tenantId, Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default);
    Task<NumericEntitlementResult> RequireNumericAsync(Guid? tenantId, Guid userId, string productCode, string featureKey,
        long requiredValue, CancellationToken cancellationToken = default);
}
