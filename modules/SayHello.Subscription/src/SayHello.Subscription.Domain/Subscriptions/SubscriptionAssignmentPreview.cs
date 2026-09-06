using System;
using System.Collections.Generic;
using System.Linq;

namespace SayHello.Subscription.Subscriptions;

public sealed class SubscriptionAssignmentPreviewItem
{
    public Guid ProductId { get; }
    public string ProductCode { get; }
    public string ProductName { get; }
    public string ProductConcurrencyStamp { get; }
    public Guid PlanId { get; }
    public string PlanCode { get; }
    public string PlanName { get; }
    public string PlanConcurrencyStamp { get; }
    public SubscriptionVersion? ExpectedCurrent { get; }
    public DateTime? CurrentExpiresAt { get; }
    public IReadOnlyList<EntitlementSnapshotData> Entitlements { get; }

    public SubscriptionAssignmentPreviewItem(Guid productId, string productCode, string productName,
        string productConcurrencyStamp, Guid planId, string planCode, string planName, string planConcurrencyStamp,
        SubscriptionVersion? expectedCurrent, DateTime? currentExpiresAt, IEnumerable<EntitlementSnapshotData> entitlements)
    {
        ProductId = productId;
        ProductCode = productCode;
        ProductName = productName;
        ProductConcurrencyStamp = productConcurrencyStamp;
        PlanId = planId;
        PlanCode = planCode;
        PlanName = planName;
        PlanConcurrencyStamp = planConcurrencyStamp;
        ExpectedCurrent = expectedCurrent;
        CurrentExpiresAt = currentExpiresAt;
        Entitlements = Array.AsReadOnly(entitlements.ToArray());
    }
}

public sealed class SubscriptionAssignmentPreview
{
    public Guid? TenantId { get; }
    public Guid UserId { get; }
    public Guid? BundleId { get; }
    public string? BundleConcurrencyStamp { get; }
    public IReadOnlyList<SubscriptionAssignmentPreviewItem> Items { get; }

    public SubscriptionAssignmentPreview(Guid? tenantId, Guid userId, Guid? bundleId, string? bundleConcurrencyStamp,
        IEnumerable<SubscriptionAssignmentPreviewItem> items)
    {
        TenantId = tenantId;
        UserId = userId;
        BundleId = bundleId;
        BundleConcurrencyStamp = bundleConcurrencyStamp;
        Items = Array.AsReadOnly(items.ToArray());
    }
}
