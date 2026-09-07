using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.Subscription.Catalog;

public interface IDefaultSubscriptionPlanRepository
{
    Task<DefaultSubscriptionPlan?> FindAsync(Guid? tenantId, string productCode,
        CancellationToken cancellationToken = default);
    Task<SubscriptionPage<DefaultSubscriptionPlan>> GetPageAsync(SubscriptionCatalogQuery query, Guid userId, DateTime now,
        CancellationToken cancellationToken = default);
}
