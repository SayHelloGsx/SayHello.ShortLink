using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Entitlements;

namespace SayHello.Subscription.Admin.Catalog;

public class AdminCatalogQueryDto : SubscriptionPagedInput
{
    [StringLength(SubscriptionConsts.MaxNameLength)]
    public string? Filter { get; set; }
    [EnumDataType(typeof(SubscriptionCatalogState))]
    public SubscriptionCatalogState? State { get; set; }
    public Guid? ProductId { get; set; }
    [EnumDataType(typeof(SubscriptionCatalogSort))]
    public SubscriptionCatalogSort Sorting { get; set; }
}

public class CatalogDetailsDto
{
    [Required, StringLength(SubscriptionConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;
    [StringLength(SubscriptionConsts.MaxDescriptionLength)]
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

public class VersionInputDto
{
    [Required, StringLength(SubscriptionConsts.MaxConcurrencyStampLength)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class CatalogStateInputDto : VersionInputDto, IValidatableObject
{
    public SubscriptionCatalogState State { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (State is not (SubscriptionCatalogState.Published or SubscriptionCatalogState.Withdrawn or SubscriptionCatalogState.Archived))
        {
            yield return AdminValidation.Error(validationContext, "Invalid catalog state.", nameof(State));
        }
    }
}

public class CreateProductDto : CatalogDetailsDto
{
    [Required, StringLength(SubscriptionConsts.MaxCodeLength)]
    public string Code { get; set; } = string.Empty;
}

public class UpdateProductDto : CatalogDetailsDto
{
    [Required, StringLength(SubscriptionConsts.MaxConcurrencyStampLength)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class CreatePlanDto : CreateProductDto, IValidatableObject
{
    public Guid ProductId { get; set; }
    [Required]
    public List<EntitlementInputDto> Entitlements { get; set; } = new();
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductId == Guid.Empty)
            yield return AdminValidation.Error(validationContext, "Select a product.", nameof(ProductId));
    }
}

public class UpdatePlanDto : UpdateProductDto
{
    [Required]
    public List<EntitlementInputDto> Entitlements { get; set; } = new();
}

public class CreateBundleDto : CreateProductDto, IValidatableObject
{
    [Required, MinLength(2)]
    public List<Guid> PlanIds { get; set; } = new();
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        BundleValidation.Validate(PlanIds, validationContext);
}

public class UpdateBundleDto : UpdateProductDto, IValidatableObject
{
    [Required, MinLength(2)]
    public List<Guid> PlanIds { get; set; } = new();
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        BundleValidation.Validate(PlanIds, validationContext);
}

internal static class BundleValidation
{
    internal static IEnumerable<ValidationResult> Validate(List<Guid>? plans, ValidationContext context)
    {
        if (plans == null || plans.Count < 2 || plans.Contains(Guid.Empty) || plans.Distinct().Count() != plans.Count)
            yield return AdminValidation.Error(context, "Select at least two distinct plans from different products.", "PlanIds");
    }
}

public class AdminProductDto : SubscriptionProductDto
{
    public SubscriptionCatalogState State { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdminPlanDto : SubscriptionPlanDto
{
    public SubscriptionCatalogState State { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdminBundleDto : SubscriptionBundleDto
{
    public SubscriptionCatalogState State { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class RegisteredProductDto
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<RegisteredFeatureDto> Features { get; set; } = new();
}

public class RegisteredFeatureDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SubscriptionEntitlementType Type { get; set; }
    public long? Maximum { get; set; }
    public bool AllowUnlimited { get; set; }
}
