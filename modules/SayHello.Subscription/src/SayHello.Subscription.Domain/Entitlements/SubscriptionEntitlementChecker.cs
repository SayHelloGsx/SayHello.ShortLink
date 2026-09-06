using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace SayHello.Subscription.Entitlements;

public class SubscriptionEntitlementChecker : DomainService, ISubscriptionEntitlementChecker
{
    private readonly ISubscriptionDefinitionRegistry _definitions;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly ICurrentTenant _tenant;
    private readonly IClock _clock;

    public SubscriptionEntitlementChecker(ISubscriptionDefinitionRegistry definitions, IUserSubscriptionRepository subscriptions,
        ICurrentTenant tenant, IClock clock)
    {
        _definitions = definitions;
        _subscriptions = subscriptions;
        _tenant = tenant;
        _clock = clock;
    }

    public virtual Task<UserSubscription?> FindEffectiveSubscriptionAsync(Guid? tenantId, Guid userId, string productCode,
        CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_tenant.Id, tenantId);
        SubscriptionGuard.Id(userId, nameof(userId));
        var product = _definitions.GetProduct(productCode);
        return _subscriptions.FindEffectiveAsync(tenantId, userId, product.Code, _clock.Now.ToUniversalTime(), cancellationToken);
    }

    public virtual async Task<BooleanEntitlementResult> GetBooleanAsync(Guid? tenantId, Guid userId, string productCode,
        string featureKey, CancellationToken cancellationToken = default)
    {
        var feature = Feature(productCode, featureKey, SubscriptionEntitlementType.Boolean);
        var subscription = await FindEffectiveSubscriptionAsync(tenantId, userId, productCode, cancellationToken);
        if (subscription == null) return BooleanEntitlementResult.NoSubscription();
        var value = subscription.Entitlements.SingleOrDefault(x => x.FeatureKey == feature.Key)?.ToValue();
        if (value != null && value.Type != feature.Type)
            throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch);
        return BooleanEntitlementResult.FromSubscription(subscription.Id, value?.BooleanValue == true);
    }

    public virtual async Task RequireBooleanAsync(Guid? tenantId, Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default)
    {
        var result = await GetBooleanAsync(tenantId, userId, productCode, featureKey, cancellationToken);
        if (!result.IsGranted) Denied(result.Status);
    }

    public virtual async Task<NumericEntitlementResult> GetNumericAsync(Guid? tenantId, Guid userId, string productCode,
        string featureKey, CancellationToken cancellationToken = default)
    {
        var feature = Feature(productCode, featureKey, SubscriptionEntitlementType.Numeric);
        var subscription = await FindEffectiveSubscriptionAsync(tenantId, userId, productCode, cancellationToken);
        if (subscription == null) return NumericEntitlementResult.NoSubscription();
        var value = subscription.Entitlements.SingleOrDefault(x => x.FeatureKey == feature.Key)?.ToValue();
        if (value == null) return NumericEntitlementResult.NotGranted(subscription.Id);
        if (value.Type != feature.Type)
            throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch);
        return value.IsUnlimited
            ? NumericEntitlementResult.Unlimited(subscription.Id)
            : NumericEntitlementResult.Finite(subscription.Id, value.NumericValue!.Value);
    }

    public virtual async Task<NumericEntitlementResult> RequireNumericAsync(Guid? tenantId, Guid userId, string productCode,
        string featureKey, long requiredValue, CancellationToken cancellationToken = default)
    {
        if (requiredValue < 0) throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
        var result = await GetNumericAsync(tenantId, userId, productCode, featureKey, cancellationToken);
        if (!result.Allows(requiredValue)) Denied(result.Status);
        return result;
    }

    private FeatureDefinition Feature(string productCode, string key, SubscriptionEntitlementType type)
    {
        var feature = _definitions.GetFeature(productCode, key);
        if (feature.Type != type) throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch);
        return feature;
    }

    private static void Denied(EntitlementGrantStatus status) =>
        throw new BusinessException(status == EntitlementGrantStatus.NoSubscription
            ? SubscriptionErrorCodes.NoEffectiveSubscription
            : SubscriptionErrorCodes.EntitlementNotGranted);
}
