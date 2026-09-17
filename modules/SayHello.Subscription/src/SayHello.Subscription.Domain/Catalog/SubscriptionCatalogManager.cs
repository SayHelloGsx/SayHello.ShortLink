using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class SubscriptionCatalogManager : DomainService, ISubscriptionCatalogManager
{
    private readonly ISubscriptionProductRepository _products;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly ISubscriptionBundleRepository _bundles;
    private readonly ISubscriptionDefinitionRegistry _definitions;
    private readonly IEnumerable<ISubscriptionEntitlementOptionProvider> _optionProviders;
    private readonly ICurrentTenant _tenant;
    private readonly IGuidGenerator _guids;
    private readonly SubscriptionTransactionRunner _transactions;
    private readonly SubscriptionMutationLock _mutationLock;

    public SubscriptionCatalogManager(ISubscriptionProductRepository products, ISubscriptionPlanRepository plans,
        ISubscriptionBundleRepository bundles, ISubscriptionDefinitionRegistry definitions, ICurrentTenant tenant,
        IEnumerable<ISubscriptionEntitlementOptionProvider> optionProviders, IGuidGenerator guids,
        SubscriptionTransactionRunner transactions, SubscriptionMutationLock mutationLock)
    {
        _products = products;
        _plans = plans;
        _bundles = bundles;
        _definitions = definitions;
        _optionProviders = optionProviders;
        _tenant = tenant;
        _guids = guids;
        _transactions = transactions;
        _mutationLock = mutationLock;
    }

    public virtual Task<SubscriptionProduct> CreateProductAsync(string registeredProductCode,
        CatalogDetails details, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var definition = _definitions.GetProduct(registeredProductCode);
            if (await _products.FindByCodeAsync(definition.Code, cancellationToken) != null)
                throw new BusinessException(SubscriptionErrorCodes.DuplicateCode);
            return await _products.InsertAsync(new SubscriptionProduct(_guids.Create(), _tenant.Id, definition,
                details.Name, details.Description, details.DisplayOrder), true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionProduct> UpdateProductAsync(Guid id, string concurrencyStamp,
        CatalogDetails details, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var product = await GetProductAsync(id, concurrencyStamp, cancellationToken);
            product.UpdateDetails(details.Name, details.Description, details.DisplayOrder);
            return await _products.UpdateAsync(product, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionProduct> SetDefaultPlanAsync(Guid productId, string concurrencyStamp,
        Guid? planId, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var product = await GetProductAsync(productId, concurrencyStamp, cancellationToken);
            var plan = planId.HasValue
                ? await _plans.GetAsync(SubscriptionGuard.Id(planId.Value, nameof(planId)), cancellationToken: cancellationToken)
                : null;
            product.SetDefaultPlan(plan);
            if (plan != null)
            {
                await ValidateStoredEntitlementsAsync(plan, cancellationToken);
            }
            return await _products.UpdateAsync(product, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionProduct> SetProductStateAsync(Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var product = await GetProductAsync(id, concurrencyStamp, cancellationToken);
            switch (state)
            {
                case SubscriptionCatalogState.Published: _definitions.GetProduct(product.Code); product.Publish(); break;
                case SubscriptionCatalogState.Withdrawn: product.Withdraw(); break;
                case SubscriptionCatalogState.Archived: product.Archive(); break;
                default: throw new BusinessException(SubscriptionErrorCodes.InvalidState);
            }
            return await _products.UpdateAsync(product, true, cancellationToken);
        }, cancellationToken);

    public virtual Task DeleteProductAsync(Guid id, string concurrencyStamp, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var product = await GetProductAsync(id, concurrencyStamp, cancellationToken);
            product.EnsureNoDefaultPlan();
            if (await _products.IsReferencedAsync(id, cancellationToken))
                throw new BusinessException(SubscriptionErrorCodes.CatalogReferenced);
            await _products.DeleteAsync(product, true, cancellationToken);
            return product;
        }, cancellationToken);

    public virtual Task<SubscriptionPlan> CreatePlanAsync(Guid productId, string code, CatalogDetails details,
        IReadOnlyDictionary<string, EntitlementValue> entitlements, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var product = await _products.GetAsync(productId, cancellationToken: cancellationToken);
            code = SubscriptionCode.Normalize(code);
            if (await _plans.FindByCodeAsync(productId, code, cancellationToken) != null)
                throw new BusinessException(SubscriptionErrorCodes.DuplicateCode);
            var definition = _definitions.GetProduct(product.Code);
            await ValidateEntitlementsAsync(definition, entitlements, null, false, cancellationToken);
            var plan = new SubscriptionPlan(_guids.Create(), product, code, details.Name, details.Description, details.DisplayOrder);
            plan.ReplaceEntitlements(definition, entitlements);
            return await _plans.InsertAsync(plan, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionPlan> UpdatePlanAsync(Guid id, string concurrencyStamp,
        CatalogDetails details, IReadOnlyDictionary<string, EntitlementValue> entitlements,
        CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var plan = await GetPlanAsync(id, concurrencyStamp, cancellationToken);
            var definition = _definitions.GetProduct(plan.ProductCode);
            var existing = plan.Entitlements.ToDictionary(
                entitlement => entitlement.FeatureKey, entitlement => entitlement.ToValue(), StringComparer.Ordinal);
            await ValidateEntitlementsAsync(definition, entitlements, existing, true, cancellationToken);
            plan.ReplaceEntitlements(definition, entitlements);
            plan.UpdateDetails(details.Name, details.Description, details.DisplayOrder);
            return await _plans.UpdateAsync(plan, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionPlan> SetPlanStateAsync(Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var plan = await GetPlanAsync(id, concurrencyStamp, cancellationToken);
            if (state is SubscriptionCatalogState.Withdrawn or SubscriptionCatalogState.Archived)
                await EnsureNotDefaultAsync(plan, cancellationToken);
            switch (state)
            {
                case SubscriptionCatalogState.Published:
                    await ValidateStoredEntitlementsAsync(plan, cancellationToken);
                    plan.Publish(await _products.GetAsync(plan.ProductId, cancellationToken: cancellationToken),
                        _definitions.GetProduct(plan.ProductCode)); break;
                case SubscriptionCatalogState.Withdrawn: plan.Withdraw(); break;
                case SubscriptionCatalogState.Archived: plan.Archive(); break;
                default: throw new BusinessException(SubscriptionErrorCodes.InvalidState);
            }
            return await _plans.UpdateAsync(plan, true, cancellationToken);
        }, cancellationToken);

    public virtual Task DeletePlanAsync(Guid id, string concurrencyStamp, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var plan = await GetPlanAsync(id, concurrencyStamp, cancellationToken);
            await EnsureNotDefaultAsync(plan, cancellationToken);
            if (await _plans.IsReferencedAsync(id, cancellationToken))
                throw new BusinessException(SubscriptionErrorCodes.CatalogReferenced);
            await _plans.DeleteAsync(plan, true, cancellationToken);
            return plan;
        }, cancellationToken);

    public virtual Task<SubscriptionBundle> CreateBundleAsync(string code, CatalogDetails details,
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            code = SubscriptionCode.Normalize(code);
            if (await _bundles.FindByCodeAsync(code, cancellationToken) != null)
                throw new BusinessException(SubscriptionErrorCodes.DuplicateCode);
            var plans = await GetBundlePlansAsync(planIds, cancellationToken);
            return await _bundles.InsertAsync(new SubscriptionBundle(_guids.Create(), _tenant.Id, code,
                details.Name, plans, details.Description, details.DisplayOrder), true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionBundle> UpdateBundleAsync(Guid id, string concurrencyStamp,
        CatalogDetails details, IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var bundle = await GetBundleAsync(id, concurrencyStamp, cancellationToken);
            bundle.ReplaceItems(await GetBundlePlansAsync(planIds, cancellationToken));
            bundle.UpdateDetails(details.Name, details.Description, details.DisplayOrder);
            return await _bundles.UpdateAsync(bundle, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionBundle> SetBundleStateAsync(Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var bundle = await GetBundleAsync(id, concurrencyStamp, cancellationToken);
            switch (state)
            {
                case SubscriptionCatalogState.Published:
                    var plans = await _plans.GetByIdsAsync(bundle.Items.Select(x => x.PlanId).ToArray(), cancellationToken);
                    var products = await _products.GetByIdsAsync(bundle.Items.Select(x => x.ProductId).ToArray(), cancellationToken);
                    foreach (var plan in plans)
                        await ValidateStoredEntitlementsAsync(plan, cancellationToken);
                    bundle.Publish(plans, products); break;
                case SubscriptionCatalogState.Withdrawn: bundle.Withdraw(); break;
                case SubscriptionCatalogState.Archived: bundle.Archive(); break;
                default: throw new BusinessException(SubscriptionErrorCodes.InvalidState);
            }
            return await _bundles.UpdateAsync(bundle, true, cancellationToken);
        }, cancellationToken);

    public virtual Task DeleteBundleAsync(Guid id, string concurrencyStamp, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async unit =>
        {
            await LockCatalogAsync(unit, cancellationToken);
            var bundle = await GetBundleAsync(id, concurrencyStamp, cancellationToken);
            if (await _bundles.IsReferencedAsync(id, cancellationToken))
                throw new BusinessException(SubscriptionErrorCodes.CatalogReferenced);
            await _bundles.DeleteAsync(bundle, true, cancellationToken);
            return bundle;
        }, cancellationToken);

    private async Task<SubscriptionProduct> GetProductAsync(Guid id, string stamp, CancellationToken token)
    {
        var entity = await _products.GetAsync(id, cancellationToken: token);
        CheckStamp(entity.ConcurrencyStamp, stamp);
        return entity;
    }

    private Task LockCatalogAsync(Volo.Abp.Uow.IUnitOfWork unit, CancellationToken token)
    {
        return _mutationLock.AcquireCatalogAsync(unit, token);
    }

    private async Task EnsureNotDefaultAsync(SubscriptionPlan plan, CancellationToken token)
    {
        var product = await _products.GetAsync(plan.ProductId, cancellationToken: token);
        if (product.DefaultPlanId == plan.Id)
            throw new BusinessException(SubscriptionErrorCodes.DefaultPlanInUse);
    }

    private async Task<SubscriptionPlan> GetPlanAsync(Guid id, string stamp, CancellationToken token)
    {
        var entity = await _plans.GetAsync(id, cancellationToken: token);
        CheckStamp(entity.ConcurrencyStamp, stamp);
        return entity;
    }

    private async Task<SubscriptionBundle> GetBundleAsync(Guid id, string stamp, CancellationToken token)
    {
        var entity = await _bundles.GetAsync(id, cancellationToken: token);
        CheckStamp(entity.ConcurrencyStamp, stamp);
        return entity;
    }

    private async Task<IReadOnlyList<SubscriptionPlan>> GetBundlePlansAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        if (ids.Count < 2 || ids.Distinct().Count() != ids.Count)
            throw new BusinessException(SubscriptionErrorCodes.InvalidBundle);
        var plans = await _plans.GetByIdsAsync(ids, token);
        if (plans.Count != ids.Count)
            throw new BusinessException(SubscriptionErrorCodes.InvalidBundle);
        return plans;
    }

    private async Task ValidateStoredEntitlementsAsync(
        SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        var definition = _definitions.GetProduct(plan.ProductCode);
        foreach (var entitlement in plan.Entitlements)
        {
            var feature = definition.GetFeature(entitlement.FeatureKey);
            await _optionProviders.ValidateOptionsAsync(
                definition.Code, feature, entitlement.ToValue(), cancellationToken);
        }
    }

    private async Task ValidateEntitlementsAsync(
        ProductDefinition definition,
        IReadOnlyDictionary<string, EntitlementValue> values,
        IReadOnlyDictionary<string, EntitlementValue>? existing,
        bool allowUnchangedStaleValues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var (key, value) in values)
        {
            var feature = definition.GetFeature(key);
            feature.Validate(value);
            if (allowUnchangedStaleValues &&
                existing != null &&
                existing.TryGetValue(feature.Key, out var oldValue) &&
                oldValue.HasSameValueAs(value))
            {
                continue;
            }

            if (allowUnchangedStaleValues &&
                existing != null &&
                existing.TryGetValue(feature.Key, out oldValue) &&
                feature.Type == SubscriptionEntitlementType.StringSet &&
                oldValue.Type == SubscriptionEntitlementType.StringSet)
            {
                var options = await _optionProviders.GetCanonicalOptionsAsync(
                    definition.Code,
                    feature,
                    cancellationToken);
                if (options.Count == 0)
                {
                    continue;
                }

                var oldItems = oldValue.StringValues!.ToHashSet(StringComparer.Ordinal);
                var invalidAddition = value.StringValues!
                    .Where(item => !oldItems.Contains(item))
                    .Any(item => !options.Contains(item, StringComparer.Ordinal));
                if (invalidAddition)
                {
                    throw new BusinessException(SubscriptionErrorCodes.EntitlementOptionNotAllowed)
                        .WithData("ProductCode", definition.Code)
                        .WithData("FeatureKey", feature.Key);
                }

                continue;
            }

            await _optionProviders.ValidateOptionsAsync(
                definition.Code, feature, value, cancellationToken);
        }
    }

    internal static void CheckStamp(string actual, string expected)
    {
        if (!string.Equals(actual, SubscriptionGuard.ConcurrencyStamp(expected), StringComparison.Ordinal))
            throw new BusinessException(SubscriptionErrorCodes.ConcurrencyConflict);
    }
}
