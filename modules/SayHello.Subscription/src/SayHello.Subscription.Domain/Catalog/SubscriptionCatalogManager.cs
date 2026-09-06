using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.Subscription.Definitions;
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
    private readonly ICurrentTenant _tenant;
    private readonly IGuidGenerator _guids;
    private readonly SubscriptionTransactionRunner _transactions;

    public SubscriptionCatalogManager(ISubscriptionProductRepository products, ISubscriptionPlanRepository plans,
        ISubscriptionBundleRepository bundles, ISubscriptionDefinitionRegistry definitions, ICurrentTenant tenant,
        IGuidGenerator guids, SubscriptionTransactionRunner transactions)
    {
        _products = products;
        _plans = plans;
        _bundles = bundles;
        _definitions = definitions;
        _tenant = tenant;
        _guids = guids;
        _transactions = transactions;
    }

    public virtual Task<SubscriptionProduct> CreateProductAsync(Guid? tenantId, string registeredProductCode,
        CatalogDetails details, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            EnsureTenant(tenantId);
            var definition = _definitions.GetProduct(registeredProductCode);
            if (await _products.FindByCodeAsync(tenantId, definition.Code, cancellationToken) != null)
                throw new BusinessException(SubscriptionErrorCodes.DuplicateCode);
            return await _products.InsertAsync(new SubscriptionProduct(_guids.Create(), tenantId, definition,
                details.Name, details.Description, details.DisplayOrder), true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionProduct> UpdateProductAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        CatalogDetails details, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var product = await GetProductAsync(tenantId, id, concurrencyStamp, cancellationToken);
            product.UpdateDetails(details.Name, details.Description, details.DisplayOrder);
            return await _products.UpdateAsync(product, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionProduct> SetProductStateAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var product = await GetProductAsync(tenantId, id, concurrencyStamp, cancellationToken);
            switch (state)
            {
                case SubscriptionCatalogState.Published: _definitions.GetProduct(product.Code); product.Publish(); break;
                case SubscriptionCatalogState.Withdrawn: product.Withdraw(); break;
                case SubscriptionCatalogState.Archived: product.Archive(); break;
                default: throw new BusinessException(SubscriptionErrorCodes.InvalidState);
            }
            return await _products.UpdateAsync(product, true, cancellationToken);
        }, cancellationToken);

    public virtual Task DeleteProductAsync(Guid? tenantId, Guid id, string concurrencyStamp, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var product = await GetProductAsync(tenantId, id, concurrencyStamp, cancellationToken);
            if (await _products.IsReferencedAsync(tenantId, id, cancellationToken))
                throw new BusinessException(SubscriptionErrorCodes.CatalogReferenced);
            await _products.DeleteAsync(product, true, cancellationToken);
            return product;
        }, cancellationToken);

    public virtual Task<SubscriptionPlan> CreatePlanAsync(Guid? tenantId, Guid productId, string code, CatalogDetails details,
        IReadOnlyDictionary<string, EntitlementValue> entitlements, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            EnsureTenant(tenantId);
            var product = await _products.GetAsync(productId, cancellationToken: cancellationToken);
            code = SubscriptionCode.Normalize(code);
            if (await _plans.FindByCodeAsync(tenantId, productId, code, cancellationToken) != null)
                throw new BusinessException(SubscriptionErrorCodes.DuplicateCode);
            var plan = new SubscriptionPlan(_guids.Create(), product, code, details.Name, details.Description, details.DisplayOrder);
            plan.ReplaceEntitlements(_definitions.GetProduct(product.Code), entitlements);
            return await _plans.InsertAsync(plan, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionPlan> UpdatePlanAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        CatalogDetails details, IReadOnlyDictionary<string, EntitlementValue> entitlements,
        CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var plan = await GetPlanAsync(tenantId, id, concurrencyStamp, cancellationToken);
            plan.ReplaceEntitlements(_definitions.GetProduct(plan.ProductCode), entitlements);
            plan.UpdateDetails(details.Name, details.Description, details.DisplayOrder);
            return await _plans.UpdateAsync(plan, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionPlan> SetPlanStateAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var plan = await GetPlanAsync(tenantId, id, concurrencyStamp, cancellationToken);
            switch (state)
            {
                case SubscriptionCatalogState.Published:
                    plan.Publish(await _products.GetAsync(plan.ProductId, cancellationToken: cancellationToken),
                        _definitions.GetProduct(plan.ProductCode)); break;
                case SubscriptionCatalogState.Withdrawn: plan.Withdraw(); break;
                case SubscriptionCatalogState.Archived: plan.Archive(); break;
                default: throw new BusinessException(SubscriptionErrorCodes.InvalidState);
            }
            return await _plans.UpdateAsync(plan, true, cancellationToken);
        }, cancellationToken);

    public virtual Task DeletePlanAsync(Guid? tenantId, Guid id, string concurrencyStamp, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var plan = await GetPlanAsync(tenantId, id, concurrencyStamp, cancellationToken);
            if (await _plans.IsReferencedAsync(tenantId, id, cancellationToken))
                throw new BusinessException(SubscriptionErrorCodes.CatalogReferenced);
            await _plans.DeleteAsync(plan, true, cancellationToken);
            return plan;
        }, cancellationToken);

    public virtual Task<SubscriptionBundle> CreateBundleAsync(Guid? tenantId, string code, CatalogDetails details,
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            EnsureTenant(tenantId);
            code = SubscriptionCode.Normalize(code);
            if (await _bundles.FindByCodeAsync(tenantId, code, cancellationToken) != null)
                throw new BusinessException(SubscriptionErrorCodes.DuplicateCode);
            var plans = await GetBundlePlansAsync(tenantId, planIds, cancellationToken);
            return await _bundles.InsertAsync(new SubscriptionBundle(_guids.Create(), tenantId, code,
                details.Name, plans, details.Description, details.DisplayOrder), true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionBundle> UpdateBundleAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        CatalogDetails details, IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var bundle = await GetBundleAsync(tenantId, id, concurrencyStamp, cancellationToken);
            bundle.ReplaceItems(await GetBundlePlansAsync(tenantId, planIds, cancellationToken));
            bundle.UpdateDetails(details.Name, details.Description, details.DisplayOrder);
            return await _bundles.UpdateAsync(bundle, true, cancellationToken);
        }, cancellationToken);

    public virtual Task<SubscriptionBundle> SetBundleStateAsync(Guid? tenantId, Guid id, string concurrencyStamp,
        SubscriptionCatalogState state, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var bundle = await GetBundleAsync(tenantId, id, concurrencyStamp, cancellationToken);
            switch (state)
            {
                case SubscriptionCatalogState.Published:
                    var plans = await _plans.GetByIdsAsync(tenantId, bundle.Items.Select(x => x.PlanId).ToArray(), cancellationToken);
                    var products = await _products.GetByIdsAsync(tenantId, bundle.Items.Select(x => x.ProductId).ToArray(), cancellationToken);
                    foreach (var plan in plans)
                        foreach (var value in plan.Entitlements)
                            _definitions.GetFeature(plan.ProductCode, value.FeatureKey).Validate(value.ToValue());
                    bundle.Publish(plans, products); break;
                case SubscriptionCatalogState.Withdrawn: bundle.Withdraw(); break;
                case SubscriptionCatalogState.Archived: bundle.Archive(); break;
                default: throw new BusinessException(SubscriptionErrorCodes.InvalidState);
            }
            return await _bundles.UpdateAsync(bundle, true, cancellationToken);
        }, cancellationToken);

    public virtual Task DeleteBundleAsync(Guid? tenantId, Guid id, string concurrencyStamp, CancellationToken cancellationToken = default) =>
        _transactions.RunAsync(async _ =>
        {
            var bundle = await GetBundleAsync(tenantId, id, concurrencyStamp, cancellationToken);
            if (await _bundles.IsReferencedAsync(tenantId, id, cancellationToken))
                throw new BusinessException(SubscriptionErrorCodes.CatalogReferenced);
            await _bundles.DeleteAsync(bundle, true, cancellationToken);
            return bundle;
        }, cancellationToken);

    private async Task<SubscriptionProduct> GetProductAsync(Guid? tenantId, Guid id, string stamp, CancellationToken token)
    {
        EnsureTenant(tenantId);
        var entity = await _products.GetAsync(id, cancellationToken: token);
        CheckStamp(entity.ConcurrencyStamp, stamp);
        return entity;
    }

    private async Task<SubscriptionPlan> GetPlanAsync(Guid? tenantId, Guid id, string stamp, CancellationToken token)
    {
        EnsureTenant(tenantId);
        var entity = await _plans.GetAsync(id, cancellationToken: token);
        CheckStamp(entity.ConcurrencyStamp, stamp);
        return entity;
    }

    private async Task<SubscriptionBundle> GetBundleAsync(Guid? tenantId, Guid id, string stamp, CancellationToken token)
    {
        EnsureTenant(tenantId);
        var entity = await _bundles.GetAsync(id, cancellationToken: token);
        CheckStamp(entity.ConcurrencyStamp, stamp);
        return entity;
    }

    private async Task<IReadOnlyList<SubscriptionPlan>> GetBundlePlansAsync(Guid? tenantId,
        IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        if (ids.Count < 2 || ids.Distinct().Count() != ids.Count)
            throw new BusinessException(SubscriptionErrorCodes.InvalidBundle);
        var plans = await _plans.GetByIdsAsync(tenantId, ids, token);
        if (plans.Count != ids.Count)
            throw new BusinessException(SubscriptionErrorCodes.InvalidBundle);
        return plans;
    }

    private void EnsureTenant(Guid? tenantId) => SubscriptionGuard.SameTenant(_tenant.Id, tenantId);

    internal static void CheckStamp(string actual, string expected)
    {
        if (!string.Equals(actual, SubscriptionGuard.ConcurrencyStamp(expected), StringComparison.Ordinal))
            throw new BusinessException(SubscriptionErrorCodes.ConcurrencyConflict);
    }
}
