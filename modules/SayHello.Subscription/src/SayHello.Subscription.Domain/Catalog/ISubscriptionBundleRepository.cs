using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.Subscription.Catalog;

/// <summary>All reads materialize component children. Reference checks include assignment history.</summary>
public interface ISubscriptionBundleRepository : IBasicRepository<SubscriptionBundle, Guid>
{
    Task<SubscriptionBundle?> FindByCodeAsync(Guid? tenantId, string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionBundle>> GetByIdsAsync(Guid? tenantId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
    Task<SubscriptionPage<SubscriptionBundle>> GetPageAsync(SubscriptionCatalogQuery query,
        CancellationToken cancellationToken = default);
    Task<bool> IsReferencedAsync(Guid? tenantId, Guid bundleId, CancellationToken cancellationToken = default);
}
