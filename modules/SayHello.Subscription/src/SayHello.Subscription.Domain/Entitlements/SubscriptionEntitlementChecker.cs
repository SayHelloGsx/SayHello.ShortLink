using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.Subscription.Catalog;
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
    private readonly IDefaultSubscriptionPlanRepository _defaults;

    public SubscriptionEntitlementChecker(ISubscriptionDefinitionRegistry definitions, IUserSubscriptionRepository subscriptions,
        ICurrentTenant tenant, IClock clock, IDefaultSubscriptionPlanRepository defaults)
    {
        _definitions = definitions;
        _subscriptions = subscriptions;
        _tenant = tenant;
        _clock = clock;
        _defaults = defaults;
    }

    public virtual async Task<EffectiveEntitlementContext> ResolveAsync(Guid userId, string productCode,
        CancellationToken cancellationToken = default)
    {
        var subscription = await FindEffectiveSubscriptionAsync(userId, productCode, cancellationToken);
        if (subscription != null && subscription.IsEffectiveAt(_clock.Now.ToUniversalTime()))
            return EffectiveEntitlementContext.FromSubscription(subscription);
        var product = _definitions.GetProduct(productCode);
        var plan = await _defaults.FindAsync(product.Code, cancellationToken);
        if (plan == null) return EffectiveEntitlementContext.None();
        ValidateDefault(plan, product.Code);
        return EffectiveEntitlementContext.FromDefaultPlan(plan);
    }

    public virtual async Task<SubscriptionPage<DefaultSubscriptionPlan>> GetDefaultPlansAsync(Guid userId,
        SubscriptionCatalogQuery query, CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.Id(userId, nameof(userId));
        query.Validate();
        var page = await _defaults.GetPageAsync(query, userId, _clock.Now.ToUniversalTime(), cancellationToken);
        foreach (var plan in page.Items) ValidateDefault(plan, plan.Product.Code);
        return page;
    }

    public virtual Task<UserSubscription?> FindEffectiveSubscriptionAsync(Guid userId, string productCode,
        CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.Id(userId, nameof(userId));
        var product = _definitions.GetProduct(productCode);
        return _subscriptions.FindEffectiveAsync(userId, product.Code, _clock.Now.ToUniversalTime(), cancellationToken);
    }

    public virtual async Task<BooleanEntitlementResult> GetBooleanAsync(Guid userId, string productCode,
        string featureKey, CancellationToken cancellationToken = default)
    {
        var feature = Feature(productCode, featureKey, SubscriptionEntitlementType.Boolean);
        var context = await ResolveAsync(userId, productCode, cancellationToken);
        if (context.Source == EntitlementSource.None) return BooleanEntitlementResult.NoSubscription();
        var value = Value(context, feature.Key);
        if (value != null && value.Type != feature.Type)
            throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch);
        return context.Subscription is { } subscription
            ? BooleanEntitlementResult.FromSubscription(subscription.Id, value?.BooleanValue == true, subscription.SourcePlanId)
            : BooleanEntitlementResult.FromDefaultPlan(context.DefaultPlan!.Plan.Id, value?.BooleanValue == true);
    }

    public virtual async Task RequireBooleanAsync(Guid userId, string productCode, string featureKey,
        CancellationToken cancellationToken = default)
    {
        var result = await GetBooleanAsync(userId, productCode, featureKey, cancellationToken);
        if (!result.IsGranted) Denied(result.Status);
    }

    public virtual async Task<NumericEntitlementResult> GetNumericAsync(Guid userId, string productCode,
        string featureKey, CancellationToken cancellationToken = default)
    {
        var feature = Feature(productCode, featureKey, SubscriptionEntitlementType.Numeric);
        var context = await ResolveAsync(userId, productCode, cancellationToken);
        if (context.Source == EntitlementSource.None) return NumericEntitlementResult.NoSubscription();
        var value = Value(context, feature.Key);
        if (value != null && value.Type != feature.Type)
            throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch);
        if (context.Subscription is { } subscription)
        {
            if (value == null) return NumericEntitlementResult.NotGranted(subscription.Id, subscription.SourcePlanId);
            return value.IsUnlimited
                ? NumericEntitlementResult.Unlimited(subscription.Id, subscription.SourcePlanId)
                : NumericEntitlementResult.Finite(subscription.Id, value.NumericValue!.Value, subscription.SourcePlanId);
        }
        var planId = context.DefaultPlan!.Plan.Id;
        if (value == null) return NumericEntitlementResult.NotGrantedDefaultPlan(planId);
        return value.IsUnlimited
            ? NumericEntitlementResult.UnlimitedDefaultPlan(planId)
            : NumericEntitlementResult.FiniteDefaultPlan(planId, value.NumericValue!.Value);
    }

    public virtual async Task<NumericEntitlementResult> RequireNumericAsync(Guid userId, string productCode,
        string featureKey, long requiredValue, CancellationToken cancellationToken = default)
    {
        if (requiredValue < 0) throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
        var result = await GetNumericAsync(userId, productCode, featureKey, cancellationToken);
        if (!result.Allows(requiredValue)) Denied(result.Status);
        return result;
    }

    public virtual async Task<EnumEntitlementResult> GetEnumAsync(Guid userId, string productCode,
        string featureKey, CancellationToken cancellationToken = default)
    {
        var feature = Feature(productCode, featureKey, SubscriptionEntitlementType.Enum);
        var context = await ResolveAsync(userId, productCode, cancellationToken);
        if (context.Source == EntitlementSource.None) return EnumEntitlementResult.NoSubscription();
        var value = Value(context, feature.Key);
        if (value != null && value.Type != feature.Type)
            throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch);
        if (context.Subscription is { } subscription)
        {
            return value == null
                ? EnumEntitlementResult.NotGranted(subscription.Id, subscription.SourcePlanId)
                : EnumEntitlementResult.FromSubscription(
                    subscription.Id, value.StringValue!, subscription.SourcePlanId);
        }

        var planId = context.DefaultPlan!.Plan.Id;
        return value == null
            ? EnumEntitlementResult.NotGrantedDefaultPlan(planId)
            : EnumEntitlementResult.FromDefaultPlan(planId, value.StringValue!);
    }

    public virtual async Task<EnumEntitlementResult> RequireEnumAsync(Guid userId,
        string productCode, string featureKey, string requiredValue,
        CancellationToken cancellationToken = default)
    {
        var result = await GetEnumAsync(userId, productCode, featureKey, cancellationToken);
        if (!result.Matches(requiredValue)) Denied(result.Status);
        return result;
    }

    public virtual async Task<StringSetEntitlementResult> GetStringSetAsync(Guid userId,
        string productCode, string featureKey, CancellationToken cancellationToken = default)
    {
        var feature = Feature(productCode, featureKey, SubscriptionEntitlementType.StringSet);
        var context = await ResolveAsync(userId, productCode, cancellationToken);
        if (context.Source == EntitlementSource.None) return StringSetEntitlementResult.NoSubscription();
        var value = Value(context, feature.Key);
        if (value != null && value.Type != feature.Type)
            throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch);
        if (context.Subscription is { } subscription)
        {
            return value == null
                ? StringSetEntitlementResult.NotGranted(subscription.Id, subscription.SourcePlanId)
                : StringSetEntitlementResult.FromSubscription(
                    subscription.Id, value.StringValues!, subscription.SourcePlanId);
        }

        var planId = context.DefaultPlan!.Plan.Id;
        return value == null
            ? StringSetEntitlementResult.NotGrantedDefaultPlan(planId)
            : StringSetEntitlementResult.FromDefaultPlan(planId, value.StringValues!);
    }

    public virtual Task<StringSetEntitlementResult> RequireStringSetAsync(Guid userId,
        string productCode, string featureKey, string requiredValue,
        CancellationToken cancellationToken = default) =>
        RequireStringSetAsync(userId, productCode, featureKey,
            new[] { requiredValue }, cancellationToken);

    public virtual async Task<StringSetEntitlementResult> RequireStringSetAsync(Guid userId,
        string productCode, string featureKey, IReadOnlyCollection<string> requiredValues,
        CancellationToken cancellationToken = default)
    {
        var result = await GetStringSetAsync(userId, productCode, featureKey, cancellationToken);
        if (!result.ContainsAll(requiredValues)) Denied(result.Status);
        return result;
    }

    private FeatureDefinition Feature(string productCode, string key, SubscriptionEntitlementType type)
    {
        var feature = _definitions.GetFeature(productCode, key);
        if (feature.Type != type) throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch);
        return feature;
    }

    private static EntitlementValue? Value(EffectiveEntitlementContext context, string featureKey) =>
        context.Subscription != null
            ? context.Subscription.Entitlements.SingleOrDefault(x => x.FeatureKey == featureKey)?.ToValue()
            : context.DefaultPlan?.Plan.Entitlements.SingleOrDefault(x => x.FeatureKey == featureKey)?.ToValue();

    private void ValidateDefault(DefaultSubscriptionPlan value, string productCode)
    {
        SubscriptionGuard.SameTenant(_tenant.Id, value.Product.TenantId);
        SubscriptionGuard.SameTenant(_tenant.Id, value.Plan.TenantId);
        if (value.Product.DefaultPlanId != value.Plan.Id || value.Plan.ProductId != value.Product.Id ||
            value.Product.Code != productCode || value.Plan.ProductCode != productCode ||
            value.Product.State != SubscriptionCatalogState.Published || value.Plan.State != SubscriptionCatalogState.Published)
            throw new BusinessException(SubscriptionErrorCodes.InvalidDefaultPlan);
        var definition = _definitions.GetProduct(productCode);
        foreach (var entitlement in value.Plan.Entitlements)
            definition.GetFeature(entitlement.FeatureKey).Validate(entitlement.ToValue());
    }

    private static void Denied(EntitlementGrantStatus status) =>
        throw new BusinessException(status == EntitlementGrantStatus.NoSubscription
            ? SubscriptionErrorCodes.NoEffectiveSubscription
            : SubscriptionErrorCodes.EntitlementNotGranted);
}
