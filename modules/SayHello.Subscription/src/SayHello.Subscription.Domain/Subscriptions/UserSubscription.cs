using System;
using System.Collections.Generic;
using System.Linq;
using SayHello.Subscription.Catalog;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Subscriptions;

public class UserSubscription : AuditedAggregateRoot<Guid>, IMultiTenant
{
    private readonly List<UserSubscriptionEntitlement> _entitlements = new();

    public Guid? TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid SourcePlanId { get; private set; }
    public Guid? SourceBundleId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string PlanCode { get; private set; } = string.Empty;
    public string PlanName { get; private set; } = string.Empty;
    public string? BundleCode { get; private set; }
    public string? BundleName { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public SubscriptionEndReason? EndReason { get; private set; }
    public string? EndReasonDetail { get; private set; }
    public bool IsCurrent { get; private set; }
    public IReadOnlyCollection<UserSubscriptionEntitlement> Entitlements => _entitlements.AsReadOnly();

    protected UserSubscription()
    {
    }

    public UserSubscription(Guid id, Guid userId, SubscriptionProduct product, SubscriptionPlan plan,
        IReadOnlyCollection<EntitlementSnapshotData> entitlements, DateTime now, DateTime? expiresAt,
        Guid assignmentId, SubscriptionBundle? sourceBundle = null)
        : base(SubscriptionGuard.Id(id, nameof(id)))
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(entitlements);
        plan.EnsureProduct(product);
        if (plan.State != SubscriptionCatalogState.Published)
        {
            throw new BusinessException(SubscriptionErrorCodes.CatalogUnavailable);
        }

        SubscriptionGuard.FutureExpiration(now, expiresAt);
        if (sourceBundle != null)
        {
            SubscriptionGuard.SameTenant(product.TenantId, sourceBundle.TenantId);
            if (sourceBundle.State != SubscriptionCatalogState.Published ||
                !sourceBundle.Items.Any(i => i.ProductId == product.Id && i.PlanId == plan.Id))
            {
                throw new BusinessException(SubscriptionErrorCodes.InvalidBundle);
            }
        }

        if (entitlements.Any(e => e == null) || entitlements.Count != plan.Entitlements.Count ||
            entitlements.Select(e => e.FeatureKey).Distinct(StringComparer.Ordinal).Count() != entitlements.Count ||
            entitlements.Any(e => !plan.Entitlements.Any(p => p.FeatureKey == e.FeatureKey && p.ToValue() == e.Value)))
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidAssignment);
        }

        TenantId = product.TenantId;
        UserId = SubscriptionGuard.Id(userId, nameof(userId));
        ProductId = product.Id;
        SourcePlanId = plan.Id;
        SourceBundleId = sourceBundle?.Id;
        AssignmentId = SubscriptionGuard.Id(assignmentId, nameof(assignmentId));
        ProductCode = product.Code;
        ProductName = product.Name;
        PlanCode = plan.Code;
        PlanName = plan.Name;
        BundleCode = sourceBundle?.Code;
        BundleName = sourceBundle?.Name;
        StartsAt = now;
        ExpiresAt = expiresAt;
        IsCurrent = true;
        _entitlements.AddRange(entitlements.Select(e => new UserSubscriptionEntitlement(TenantId, Id, e)));
    }

    public bool IsEffectiveAt(DateTime now)
    {
        SubscriptionGuard.Utc(now);
        return IsCurrent && EndedAt == null && StartsAt <= now && (!ExpiresAt.HasValue || ExpiresAt.Value > now);
    }

    public UserSubscriptionStatus GetStatus(DateTime now)
    {
        SubscriptionGuard.Utc(now);
        if (EndReason == SubscriptionEndReason.Replaced)
        {
            return UserSubscriptionStatus.Replaced;
        }

        if (EndReason == SubscriptionEndReason.Revoked)
        {
            return UserSubscriptionStatus.Revoked;
        }

        if (now < StartsAt)
        {
            return UserSubscriptionStatus.NotStarted;
        }

        return IsEffectiveAt(now) ? UserSubscriptionStatus.Active : UserSubscriptionStatus.Expired;
    }

    public void End(DateTime now, SubscriptionEndReason reason, string? detail = null)
    {
        SubscriptionGuard.Utc(now);
        if (!IsCurrent || EndedAt.HasValue || now < StartsAt || !Enum.IsDefined(reason))
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidState);
        }

        var checkedDetail = string.IsNullOrWhiteSpace(detail)
            ? null
            : Check.Length(detail.Trim(), nameof(detail), SubscriptionConsts.MaxReasonLength);
        IsCurrent = false;
        EndedAt = now;
        EndReason = reason;
        EndReasonDetail = checkedDetail;
    }

    public void AdjustExpiration(DateTime now, DateTime? expiresAt)
    {
        if (!IsEffectiveAt(now))
        {
            throw new BusinessException(SubscriptionErrorCodes.NoEffectiveSubscription);
        }

        SubscriptionGuard.FutureExpiration(now, expiresAt);
        ExpiresAt = expiresAt;
    }
}
