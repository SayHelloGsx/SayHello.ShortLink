using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SayHello.Subscription.Definitions;
using Volo.Abp;

namespace SayHello.Subscription.Entitlements;

public class EntitlementValueDto : IValidatableObject
{
    public SubscriptionEntitlementType Type { get; set; }
    public bool? BooleanValue { get; set; }
    public long? NumericValue { get; set; }
    public bool IsUnlimited { get; set; }
    [StringLength(SubscriptionConsts.MaxEntitlementStringLength)]
    public string? StringValue { get; set; }
    public List<string>? StringValues { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        try
        {
            EntitlementValue.FromValues(Type, BooleanValue, NumericValue, IsUnlimited, StringValue, StringValues);
            return Array.Empty<ValidationResult>();
        }
        catch (BusinessException)
        {
            return new[] { new ValidationResult("The typed entitlement value is invalid.",
                new[]
                {
                    nameof(Type), nameof(BooleanValue), nameof(NumericValue), nameof(IsUnlimited),
                    nameof(StringValue), nameof(StringValues)
                }) };
        }
    }
}

public class EntitlementInputDto
{
    [Required]
    [StringLength(SubscriptionConsts.MaxFeatureKeyLength)]
    public string FeatureKey { get; set; } = string.Empty;

    [Required]
    public EntitlementValueDto Value { get; set; } = new();
}

public class EntitlementDto : EntitlementInputDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class BooleanEntitlementResultDto
{
    public EntitlementGrantStatus Status { get; set; }
    public EntitlementSource Source { get; set; }
    public Guid? PlanId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public bool IsGranted { get; set; }
}

public class NumericEntitlementResultDto
{
    public EntitlementGrantStatus Status { get; set; }
    public EntitlementSource Source { get; set; }
    public Guid? PlanId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public bool IsGranted { get; set; }
    public long? Limit { get; set; }
    public bool IsUnlimited { get; set; }
}

public class EnumEntitlementResultDto
{
    public EntitlementGrantStatus Status { get; set; }
    public EntitlementSource Source { get; set; }
    public Guid? PlanId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public bool IsGranted { get; set; }
    public string? Value { get; set; }
}

public class StringSetEntitlementResultDto
{
    public EntitlementGrantStatus Status { get; set; }
    public EntitlementSource Source { get; set; }
    public Guid? PlanId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public bool IsGranted { get; set; }
    public List<string> Values { get; set; } = new();
}
