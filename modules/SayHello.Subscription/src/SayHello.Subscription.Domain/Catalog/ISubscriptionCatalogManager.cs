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
    Task<SubscriptionProduct> CreateProductAsync(string registeredProductCode, CatalogDetails details,
        CancellationToken cancellationToken = default);
    Task<SubscriptionProduct> UpdateProductAsync(Guid id, string concurrencyStamp, CatalogDetails details,
        CancellationToken cancellationToken = default);
    Task<SubscriptionProduct> SetDefaultPlanAsync(Guid productId, string concurrencyStamp,
        Guid? planId, CancellationToken cancellationToken = default);
    Task<SubscriptionProduct> SetProductStateAsync(Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(Guid id, string concurrencyStamp, CancellationToken cancellationToken = default);

    Task<SubscriptionPlan> CreatePlanAsync(Guid productId, string code, CatalogDetails details,
        IReadOnlyDictionary<string, EntitlementValue> entitlements, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan> UpdatePlanAsync(Guid id, string concurrencyStamp, CatalogDetails details,
        IReadOnlyDictionary<string, EntitlementValue> entitlements, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan> SetPlanStateAsync(Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default);
    Task DeletePlanAsync(Guid id, string concurrencyStamp, CancellationToken cancellationToken = default);

    Task<SubscriptionBundle> CreateBundleAsync(string code, CatalogDetails details,
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default);
    Task<SubscriptionBundle> UpdateBundleAsync(Guid id, string concurrencyStamp, CatalogDetails details,
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default);
    Task<SubscriptionBundle> SetBundleStateAsync(Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default);
    Task DeleteBundleAsync(Guid id, string concurrencyStamp, CancellationToken cancellationToken = default);
}
