using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.Subscription.Users;

/// <summary>
/// Required host adapter; no fallback. Every operation must reject a tenant different from ICurrentTenant.
/// Search validates bounded paging; all results, including batch lookups, belong to that tenant.
/// Application services authorize directory search before calling this abstraction.
/// </summary>
public interface ISubscriptionUserDirectory
{
    Task<SubscriptionUserData?> FindAsync(Guid? tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SubscriptionPage<SubscriptionUserData>> SearchAsync(Guid? tenantId, string? filter,
        int skipCount, int maxResultCount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionUserData>> GetByIdsAsync(Guid? tenantId, IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
