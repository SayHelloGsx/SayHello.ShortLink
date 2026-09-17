using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;

namespace SayHello.Subscription.Entitlements;

/// <summary>
/// Uses stored subscription snapshots, otherwise the latest configured default plan, and the ABP clock.
/// Unknown keys and type mismatches are errors even without a subscription. Require methods throw for missing sources or grants;
/// numeric requirements compare limits, enum requirements use ordinal equality, and string-set requirements require every value.
/// </summary>
public interface ISubscriptionEntitlementChecker
{
    Task<EffectiveEntitlementContext> ResolveAsync(Guid userId, string productCode,
        CancellationToken cancellationToken = default);
    Task<SubscriptionPage<DefaultSubscriptionPlan>> GetDefaultPlansAsync(Guid userId,
        SubscriptionCatalogQuery query, CancellationToken cancellationToken = default);
    Task<UserSubscription?> FindEffectiveSubscriptionAsync(Guid userId, string productCode,
        CancellationToken cancellationToken = default);
    Task<BooleanEntitlementResult> GetBooleanAsync(Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default);
    Task RequireBooleanAsync(Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default);
    Task<NumericEntitlementResult> GetNumericAsync(Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default);
    Task<NumericEntitlementResult> RequireNumericAsync(Guid userId, string productCode, string featureKey,
        long requiredValue, CancellationToken cancellationToken = default);
    Task<EnumEntitlementResult> GetEnumAsync(Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default);
    Task<EnumEntitlementResult> RequireEnumAsync(Guid userId, string productCode, string featureKey,
        string requiredValue, CancellationToken cancellationToken = default);
    Task<StringSetEntitlementResult> GetStringSetAsync(Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default);
    Task<StringSetEntitlementResult> RequireStringSetAsync(Guid userId, string productCode,
        string featureKey, string requiredValue, CancellationToken cancellationToken = default);
    Task<StringSetEntitlementResult> RequireStringSetAsync(Guid userId, string productCode,
        string featureKey, IReadOnlyCollection<string> requiredValues, CancellationToken cancellationToken = default);
}
