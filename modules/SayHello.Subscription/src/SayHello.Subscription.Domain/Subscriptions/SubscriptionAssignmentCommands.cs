using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace SayHello.Subscription.Subscriptions;

public sealed record AssignSubscriptionPlan
{
    public Guid? TenantId { get; }
    public Guid UserId { get; }
    public SubscriptionAssignmentTarget Target { get; }

    public AssignSubscriptionPlan(Guid? tenantId, Guid userId, SubscriptionAssignmentTarget target)
    {
        TenantId = tenantId;
        UserId = SubscriptionGuard.Id(userId, nameof(userId));
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }
}

public sealed record AssignSubscriptionBundle
{
    public Guid? TenantId { get; }
    public Guid UserId { get; }
    public Guid BundleId { get; }
    public string BundleConcurrencyStamp { get; }
    public IReadOnlyList<SubscriptionAssignmentTarget> Targets { get; }

    public AssignSubscriptionBundle(Guid? tenantId, Guid userId, Guid bundleId, string bundleConcurrencyStamp,
        IEnumerable<SubscriptionAssignmentTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var items = targets.ToArray();
        if (items.Length < 2 || items.Any(t => t == null) ||
            items.Select(t => t.ProductId).Distinct().Count() != items.Length)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidAssignment);
        }

        TenantId = tenantId;
        UserId = SubscriptionGuard.Id(userId, nameof(userId));
        BundleId = SubscriptionGuard.Id(bundleId, nameof(bundleId));
        BundleConcurrencyStamp = SubscriptionGuard.ConcurrencyStamp(bundleConcurrencyStamp);
        Targets = Array.AsReadOnly(items);
    }
}
