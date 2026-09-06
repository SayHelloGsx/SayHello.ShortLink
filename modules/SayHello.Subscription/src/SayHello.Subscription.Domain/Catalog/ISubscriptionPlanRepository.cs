using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.Subscription.Catalog;

/// <summary>All reads materialize entitlement children. Reference checks include bundles and assignment history.</summary>
public interface ISubscriptionPlanRepository : IBasicRepository<SubscriptionPlan, Guid>
{
    Task<SubscriptionPlan?> FindByCodeAsync(Guid? tenantId, Guid productId, string code,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionPlan>> GetByIdsAsync(Guid? tenantId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
    Task<SubscriptionPage<SubscriptionPlan>> GetPageAsync(SubscriptionCatalogQuery query,
        CancellationToken cancellationToken = default);
    Task<bool> IsReferencedAsync(Guid? tenantId, Guid planId, CancellationToken cancellationToken = default);
}
