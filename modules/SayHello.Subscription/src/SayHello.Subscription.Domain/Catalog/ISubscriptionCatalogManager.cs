using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SayHello.Subscription.Definitions;

namespace SayHello.Subscription.Catalog;

/// <summary>
/// Enforces current-tenant isolation, registered definitions, immutable code uniqueness,
/// reference-safe deletion and optimistic concurrency. State commands publish, withdraw or archive;
/// archived entries are terminal. Changing bundle components returns the bundle to Draft.
/// </summary>
public interface ISubscriptionCatalogManager
{
    Task<SubscriptionProduct> CreateProductAsync(Guid? tenantId, string registeredProductCode, CatalogDetails details,
        CancellationToken cancellationToken = default);
    Task<SubscriptionProduct> UpdateProductAsync(Guid? tenantId, Guid id, string concurrencyStamp, CatalogDetails details,
        CancellationToken cancellationToken = default);
    Task<SubscriptionProduct> SetProductStateAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(Guid? tenantId, Guid id, string concurrencyStamp, CancellationToken cancellationToken = default);

    Task<SubscriptionPlan> CreatePlanAsync(Guid? tenantId, Guid productId, string code, CatalogDetails details,
        IReadOnlyDictionary<string, EntitlementValue> entitlements, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan> UpdatePlanAsync(Guid? tenantId, Guid id, string concurrencyStamp, CatalogDetails details,
        IReadOnlyDictionary<string, EntitlementValue> entitlements, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan> SetPlanStateAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default);
    Task DeletePlanAsync(Guid? tenantId, Guid id, string concurrencyStamp, CancellationToken cancellationToken = default);

    Task<SubscriptionBundle> CreateBundleAsync(Guid? tenantId, string code, CatalogDetails details,
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default);
    Task<SubscriptionBundle> UpdateBundleAsync(Guid? tenantId, Guid id, string concurrencyStamp, CatalogDetails details,
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default);
    Task<SubscriptionBundle> SetBundleStateAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default);
    Task DeleteBundleAsync(Guid? tenantId, Guid id, string concurrencyStamp, CancellationToken cancellationToken = default);
}
