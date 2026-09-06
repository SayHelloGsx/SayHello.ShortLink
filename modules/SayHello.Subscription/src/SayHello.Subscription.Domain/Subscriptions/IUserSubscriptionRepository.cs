using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.Subscription.Subscriptions;

/// <summary>
/// Tenant-scoped materialized reads always include snapshot children. No history deletion is exposed.
/// Current-slot uniqueness must be enforced separately for null and non-null tenants, independent of expiration.
/// </summary>
public interface IUserSubscriptionRepository
{
    Task<UserSubscription> GetAsync(Guid? tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<UserSubscription> InsertAsync(UserSubscription subscription, bool autoSave = false,
        CancellationToken cancellationToken = default);
    Task<UserSubscription> UpdateAsync(UserSubscription subscription, bool autoSave = false,
        CancellationToken cancellationToken = default);
    // Current-slot reads include expired rows, because replacement must retire those slots too.
    Task<UserSubscription?> FindCurrentAsync(Guid? tenantId, Guid userId, Guid productId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscription>> GetCurrentListAsync(Guid? tenantId, Guid userId,
        IReadOnlyCollection<Guid>? productIds = null, CancellationToken cancellationToken = default);
    // Catalog withdrawal does not invalidate a snapshot; do not join to current catalog availability here.
    Task<UserSubscription?> FindEffectiveAsync(Guid? tenantId, Guid userId, string productCode, DateTime now,
        CancellationToken cancellationToken = default);
    Task<SubscriptionPage<UserSubscription>> GetPageAsync(UserSubscriptionQuery query,
        CancellationToken cancellationToken = default);
}
