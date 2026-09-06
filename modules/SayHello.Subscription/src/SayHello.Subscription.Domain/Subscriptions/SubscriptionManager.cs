using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Users;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace SayHello.Subscription.Subscriptions;

public class SubscriptionManager : DomainService, ISubscriptionManager
{
    private readonly ISubscriptionProductRepository _products;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly ISubscriptionBundleRepository _bundles;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly ISubscriptionDefinitionRegistry _definitions;
    private readonly ISubscriptionUserDirectory _users;
    private readonly ICurrentTenant _tenant;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guids;
    private readonly IStringLocalizerFactory _localizers;
    private readonly SubscriptionTransactionRunner _transactions;
    private readonly SubscriptionMutationLock _mutationLock;

    public SubscriptionManager(ISubscriptionProductRepository products, ISubscriptionPlanRepository plans,
        ISubscriptionBundleRepository bundles, IUserSubscriptionRepository subscriptions,
        ISubscriptionDefinitionRegistry definitions, ISubscriptionUserDirectory users, ICurrentTenant tenant,
        IClock clock, IGuidGenerator guids, IStringLocalizerFactory localizers,
        SubscriptionTransactionRunner transactions, SubscriptionMutationLock mutationLock)
    {
        _products = products;
        _plans = plans;
        _bundles = bundles;
        _subscriptions = subscriptions;
        _definitions = definitions;
        _users = users;
        _tenant = tenant;
        _clock = clock;
        _guids = guids;
        _localizers = localizers;
        _transactions = transactions;
        _mutationLock = mutationLock;
    }

    public virtual async Task<SubscriptionAssignmentPreview> PreviewPlanAsync(Guid? tenantId, Guid userId, Guid planId,
        CancellationToken cancellationToken = default)
    {
        await ValidateUserAsync(tenantId, userId, cancellationToken);
        var plan = (await _plans.GetByIdsAsync(tenantId, new[] { planId }, cancellationToken)).SingleOrDefault()
            ?? throw new EntityNotFoundException(typeof(SubscriptionPlan), planId);
        var product = (await _products.GetByIdsAsync(tenantId, new[] { plan.ProductId }, cancellationToken)).Single();
        ValidatePlan(product, plan);
        var current = await _subscriptions.FindCurrentAsync(tenantId, userId, product.Id, cancellationToken);
        return new SubscriptionAssignmentPreview(tenantId, userId, null, null,
            new[] { PreviewItem(product, plan, current) });
    }

    public virtual async Task<SubscriptionAssignmentPreview> PreviewBundleAsync(Guid? tenantId, Guid userId, Guid bundleId,
        CancellationToken cancellationToken = default)
    {
        await ValidateUserAsync(tenantId, userId, cancellationToken);
        var bundle = (await _bundles.GetByIdsAsync(tenantId, new[] { bundleId }, cancellationToken)).SingleOrDefault()
            ?? throw new EntityNotFoundException(typeof(SubscriptionBundle), bundleId);
        var plans = await _plans.GetByIdsAsync(tenantId, bundle.Items.Select(x => x.PlanId).ToArray(), cancellationToken);
        var products = await _products.GetByIdsAsync(tenantId, bundle.Items.Select(x => x.ProductId).ToArray(), cancellationToken);
        EnsureBundle(bundle, plans, products);
        var current = await _subscriptions.GetCurrentListAsync(tenantId, userId, products.Select(x => x.Id).ToArray(), cancellationToken);
        var items = plans.OrderBy(x => x.ProductCode, StringComparer.Ordinal).Select(plan =>
        {
            var product = products.Single(x => x.Id == plan.ProductId);
            ValidatePlan(product, plan);
            return PreviewItem(product, plan, current.SingleOrDefault(x => x.ProductId == product.Id));
        }).ToArray();
        return new SubscriptionAssignmentPreview(tenantId, userId, bundle.Id, bundle.ConcurrencyStamp, items);
    }

    public virtual async Task<UserSubscription> AssignPlanAsync(AssignSubscriptionPlan input,
        CancellationToken cancellationToken = default)
    {
        var result = await AssignAsync(input.TenantId, input.UserId, new[] { input.Target }, null, null, cancellationToken);
        return result[0];
    }

    public virtual Task<IReadOnlyList<UserSubscription>> AssignBundleAsync(AssignSubscriptionBundle input,
        CancellationToken cancellationToken = default) =>
        AssignAsync(input.TenantId, input.UserId, input.Targets, input.BundleId, input.BundleConcurrencyStamp, cancellationToken);

    private Task<IReadOnlyList<UserSubscription>> AssignAsync(Guid? tenantId, Guid userId,
        IReadOnlyList<SubscriptionAssignmentTarget> targets, Guid? bundleId, string? bundleStamp, CancellationToken token) =>
        _transactions.RunAsync<IReadOnlyList<UserSubscription>>(async unitOfWork =>
        {
            EnsureTenant(tenantId);
            await _mutationLock.AcquireAsync(unitOfWork, tenantId, userId, token);
            await ValidateUserAsync(tenantId, userId, token);
            SubscriptionBundle? bundle = null;
            if (bundleId.HasValue)
            {
                bundle = (await _bundles.GetByIdsAsync(tenantId, new[] { bundleId.Value }, token)).SingleOrDefault()
                    ?? throw new EntityNotFoundException(typeof(SubscriptionBundle), bundleId.Value);
                SubscriptionCatalogManager.CheckStamp(bundle.ConcurrencyStamp, bundleStamp!);
                if (bundle.Items.Count != targets.Count || bundle.Items.Any(item =>
                        !targets.Any(target => target.ProductId == item.ProductId && target.PlanId == item.PlanId)))
                    throw new BusinessException(SubscriptionErrorCodes.ConcurrencyConflict);
            }

            var plans = await _plans.GetByIdsAsync(tenantId, targets.Select(x => x.PlanId).ToArray(), token);
            var products = await _products.GetByIdsAsync(tenantId, targets.Select(x => x.ProductId).ToArray(), token);
            if (plans.Count != targets.Count || products.Count != targets.Count)
                throw new BusinessException(SubscriptionErrorCodes.InvalidAssignment);
            if (bundle != null) EnsureBundle(bundle, plans, products);
            var currents = await _subscriptions.GetCurrentListAsync(tenantId, userId, products.Select(x => x.Id).ToArray(), token);
            var now = _clock.Now.ToUniversalTime();
            var assignmentId = _guids.Create();
            var replacements = new List<UserSubscription>();

            foreach (var target in targets)
            {
                var product = products.Single(x => x.Id == target.ProductId);
                var plan = plans.Single(x => x.Id == target.PlanId);
                ValidatePlan(product, plan);
                SubscriptionCatalogManager.CheckStamp(product.ConcurrencyStamp, target.ProductConcurrencyStamp);
                SubscriptionCatalogManager.CheckStamp(plan.ConcurrencyStamp, target.PlanConcurrencyStamp);
                CheckExpectedCurrent(currents.SingleOrDefault(x => x.ProductId == target.ProductId), target.ExpectedCurrent);
                SubscriptionGuard.FutureExpiration(now, target.ExpiresAt);
                replacements.Add(new UserSubscription(_guids.Create(), userId, product, plan, Snapshot(plan),
                    now, target.ExpiresAt, assignmentId, bundle));
            }

            // Flush retired slots before inserting their successors; all saves remain in this transaction.
            foreach (var current in currents)
            {
                unitOfWork.Items[OwnerKey(current.Id)] = userId;
                current.End(now, SubscriptionEndReason.Replaced);
                await _subscriptions.UpdateAsync(current, true, token);
            }
            foreach (var replacement in replacements)
            {
                await _subscriptions.InsertAsync(replacement, true, token);
                unitOfWork.Items[OwnerKey(replacement.Id)] = userId;
            }
            return replacements.AsReadOnly();
        }, token);

    public virtual async Task<UserSubscription> RevokeAsync(Guid? tenantId, Guid subscriptionId, string concurrencyStamp,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var userId = await GetMutationUserIdAsync(tenantId, subscriptionId, cancellationToken);
        return await _transactions.RunAsync(async unitOfWork =>
        {
            await _mutationLock.AcquireAsync(unitOfWork, tenantId, userId, cancellationToken);
            var subscription = await _subscriptions.GetAsync(tenantId, subscriptionId, cancellationToken);
            unitOfWork.Items[OwnerKey(subscriptionId)] = userId;
            SubscriptionCatalogManager.CheckStamp(subscription.ConcurrencyStamp, concurrencyStamp);
            subscription.End(_clock.Now.ToUniversalTime(), SubscriptionEndReason.Revoked, reason);
            return await _subscriptions.UpdateAsync(subscription, true, cancellationToken);
        }, cancellationToken);
    }

    public virtual async Task<UserSubscription> AdjustExpirationAsync(Guid? tenantId, Guid subscriptionId, string concurrencyStamp,
        DateTime? expiresAt, CancellationToken cancellationToken = default)
    {
        var userId = await GetMutationUserIdAsync(tenantId, subscriptionId, cancellationToken);
        return await _transactions.RunAsync(async unitOfWork =>
        {
            await _mutationLock.AcquireAsync(unitOfWork, tenantId, userId, cancellationToken);
            var subscription = await _subscriptions.GetAsync(tenantId, subscriptionId, cancellationToken);
            unitOfWork.Items[OwnerKey(subscriptionId)] = userId;
            SubscriptionCatalogManager.CheckStamp(subscription.ConcurrencyStamp, concurrencyStamp);
            subscription.AdjustExpiration(_clock.Now.ToUniversalTime(), expiresAt);
            return await _subscriptions.UpdateAsync(subscription, true, cancellationToken);
        }, cancellationToken);
    }

    private Task<Guid> GetMutationUserIdAsync(Guid? tenantId, Guid subscriptionId, CancellationToken token)
    {
        EnsureTenant(tenantId);
        if (_transactions.Current?.Items.TryGetValue(OwnerKey(subscriptionId), out var owner) == true)
        {
            return Task.FromResult((Guid)owner);
        }
        // Resolve only the immutable owner in a separate read scope, so no database locks are held
        // while waiting for the user mutation lock. Reload and check the version after acquiring it.
        return _transactions.ReadAsync(async () => (await _subscriptions.GetAsync(tenantId, subscriptionId, token)).UserId, token);
    }

    private static string OwnerKey(Guid id) => $"Subscription:Owner:{id:N}";

    private async Task ValidateUserAsync(Guid? tenantId, Guid userId, CancellationToken token)
    {
        EnsureTenant(tenantId);
        SubscriptionGuard.Id(userId, nameof(userId));
        var user = await _users.FindAsync(tenantId, userId, token);
        if (user == null || !user.IsActive)
            throw new BusinessException(SubscriptionErrorCodes.UserNotFound);
        SubscriptionGuard.SameTenant(tenantId, user.TenantId);
        if (user.Id != userId)
            throw new BusinessException(SubscriptionErrorCodes.UserNotFound);
    }

    private void ValidatePlan(SubscriptionProduct product, SubscriptionPlan plan)
    {
        plan.EnsureProduct(product);
        if (plan.State != SubscriptionCatalogState.Published)
            throw new BusinessException(SubscriptionErrorCodes.CatalogUnavailable);
        var definition = _definitions.GetProduct(product.Code);
        foreach (var value in plan.Entitlements)
            definition.GetFeature(value.FeatureKey).Validate(value.ToValue());
    }

    private IReadOnlyList<EntitlementSnapshotData> Snapshot(SubscriptionPlan plan) =>
        plan.Entitlements.Select(value => new EntitlementSnapshotData(value.FeatureKey,
            _definitions.GetFeature(plan.ProductCode, value.FeatureKey).DisplayName.Localize(_localizers).Value,
            value.ToValue())).ToArray();

    private SubscriptionAssignmentPreviewItem PreviewItem(SubscriptionProduct product, SubscriptionPlan plan,
        UserSubscription? current) =>
        new(product.Id, product.Code, product.Name, product.ConcurrencyStamp, plan.Id, plan.Code, plan.Name,
            plan.ConcurrencyStamp, current == null ? null : new SubscriptionVersion(current.Id, current.ConcurrencyStamp),
            current?.ExpiresAt, Snapshot(plan));

    private static void EnsureBundle(SubscriptionBundle bundle, IReadOnlyList<SubscriptionPlan> plans,
        IReadOnlyList<SubscriptionProduct> products)
    {
        if (bundle.State != SubscriptionCatalogState.Published)
            throw new BusinessException(SubscriptionErrorCodes.CatalogUnavailable);
        bundle.EnsureAvailableComponents(plans, products);
    }

    private static void CheckExpectedCurrent(UserSubscription? current, SubscriptionVersion? expected)
    {
        if (current == null && expected == null) return;
        if (current == null || expected == null || current.Id != expected.SubscriptionId)
            throw new BusinessException(SubscriptionErrorCodes.ConcurrencyConflict);
        SubscriptionCatalogManager.CheckStamp(current.ConcurrencyStamp, expected.ConcurrencyStamp);
    }

    private void EnsureTenant(Guid? tenantId) => SubscriptionGuard.SameTenant(_tenant.Id, tenantId);
}
