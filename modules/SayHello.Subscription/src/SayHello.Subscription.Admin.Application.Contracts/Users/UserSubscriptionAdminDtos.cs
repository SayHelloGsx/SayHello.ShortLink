using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;

namespace SayHello.Subscription.Admin.Users;

public class UserLookupInputDto : SubscriptionPagedInput
{
    [StringLength(SubscriptionConsts.MaxNameLength)]
    public string? Filter { get; set; }
}

public class AdminSubscriptionQueryDto : UserLookupInputDto
{
    public Guid? UserId { get; set; }
    public Guid? ProductId { get; set; }
    [EnumDataType(typeof(UserSubscriptionStatus))]
    public UserSubscriptionStatus? Status { get; set; }
    public bool CurrentOnly { get; set; }
    [EnumDataType(typeof(UserSubscriptionSort))]
    public UserSubscriptionSort Sorting { get; set; }
}

public class SubscriptionUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}

public class AdminUserSubscriptionDto : UserSubscriptionDto
{
    public Guid UserId { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AssignmentTargetDto : IValidatableObject
{
    public Guid ProductId { get; set; }
    public Guid PlanId { get; set; }
    [Required, StringLength(SubscriptionConsts.MaxConcurrencyStampLength)]
    public string ProductConcurrencyStamp { get; set; } = string.Empty;
    [Required, StringLength(SubscriptionConsts.MaxConcurrencyStampLength)]
    public string PlanConcurrencyStamp { get; set; } = string.Empty;
    public SubscriptionVersionDto? ExpectedCurrent { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductId == Guid.Empty || PlanId == Guid.Empty || ExpectedCurrent?.SubscriptionId == Guid.Empty)
            yield return AdminValidation.Error(validationContext, "Invalid assignment identity.");
        if (ExpiresAt.HasValue && ExpiresAt.Value.Kind != DateTimeKind.Utc)
            yield return AdminValidation.Error(validationContext, "Expiration must use UTC.", nameof(ExpiresAt));
    }
}

public class AssignPlanDto : IValidatableObject
{
    public Guid UserId { get; set; }
    [Required]
    public AssignmentTargetDto Target { get; set; } = new();
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UserId == Guid.Empty)
            yield return AdminValidation.Error(validationContext, "Select a user.", nameof(UserId));
    }
}

public class AssignBundleDto : IValidatableObject
{
    public Guid UserId { get; set; }
    public Guid BundleId { get; set; }
    [Required, StringLength(SubscriptionConsts.MaxConcurrencyStampLength)]
    public string BundleConcurrencyStamp { get; set; } = string.Empty;
    [Required, MinLength(2)]
    public List<AssignmentTargetDto> Targets { get; set; } = new();
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UserId == Guid.Empty || BundleId == Guid.Empty)
            yield return AdminValidation.Error(validationContext, "Select a user and bundle.");
        if (Targets == null || Targets.Count < 2 || Targets.Any(t => t == null) ||
            Targets.Select(t => t.ProductId).Distinct().Count() != Targets.Count)
            yield return AdminValidation.Error(validationContext, "A bundle requires distinct products.", nameof(Targets));
    }
}

public class AssignmentPreviewItemDto : AssignmentTargetDto
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public DateTime? CurrentExpiresAt { get; set; }
    public List<EntitlementDto> Entitlements { get; set; } = new();
}

public class AssignmentPreviewDto
{
    public Guid UserId { get; set; }
    public Guid? BundleId { get; set; }
    public string? BundleConcurrencyStamp { get; set; }
    public List<AssignmentPreviewItemDto> Items { get; set; } = new();
}

public class RevokeSubscriptionDto : VersionInputDto
{
    [StringLength(SubscriptionConsts.MaxReasonLength)]
    public string? Reason { get; set; }
}

public class AdjustExpirationDto : VersionInputDto, IValidatableObject
{
    public DateTime? ExpiresAt { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpiresAt.HasValue && ExpiresAt.Value.Kind != DateTimeKind.Utc)
            yield return AdminValidation.Error(validationContext, "Expiration must use UTC.", nameof(ExpiresAt));
    }
}
