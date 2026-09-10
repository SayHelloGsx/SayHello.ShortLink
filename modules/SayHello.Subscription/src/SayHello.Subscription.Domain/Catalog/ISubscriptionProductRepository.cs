using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.Subscription.Catalog;

public interface ISubscriptionProductRepository : IBasicRepository<SubscriptionProduct, Guid>
{
    Task<SubscriptionProduct?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionProduct>> GetByIdsAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
    Task<SubscriptionPage<SubscriptionProduct>> GetPageAsync(SubscriptionCatalogQuery query,
        CancellationToken cancellationToken = default);
    Task<bool> IsReferencedAsync(Guid productId, CancellationToken cancellationToken = default);
}
