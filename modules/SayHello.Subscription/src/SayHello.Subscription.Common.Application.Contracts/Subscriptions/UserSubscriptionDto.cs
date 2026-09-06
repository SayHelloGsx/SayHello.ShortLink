using System;
using System.Collections.Generic;
using SayHello.Subscription.Entitlements;
using Volo.Abp.Application.Dtos;

namespace SayHello.Subscription.Subscriptions;

public class UserSubscriptionDto : EntityDto<Guid>
{
    public Guid ProductId { get; set; }
    public Guid SourcePlanId { get; set; }
    public Guid? SourceBundleId { get; set; }
    public Guid AssignmentId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string? BundleCode { get; set; }
    public string? BundleName { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public SubscriptionEndReason? EndReason { get; set; }
    public string? EndReasonDetail { get; set; }
    public bool IsCurrent { get; set; }
    public UserSubscriptionStatus Status { get; set; }
    public List<EntitlementDto> Entitlements { get; set; } = new();
}
