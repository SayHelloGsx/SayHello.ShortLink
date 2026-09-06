using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SayHello.Subscription.Public.Subscriptions;

public class GetMySubscriptionsInput : SubscriptionPagedInput
{
    public Guid? ProductId { get; set; }

    [StringLength(SubscriptionConsts.MaxNameLength)]
    public string? Filter { get; set; }

    [EnumDataType(typeof(UserSubscriptionStatus))]
    public UserSubscriptionStatus? Status { get; set; }

    public bool CurrentOnly { get; set; }

    [EnumDataType(typeof(UserSubscriptionSort))]
    public UserSubscriptionSort Sorting { get; set; } = UserSubscriptionSort.StartsAtDescending;

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext))
        {
            yield return result;
        }

        if (ProductId == Guid.Empty)
        {
            yield return new ValidationResult("A product identifier must not be empty.", new[] { nameof(ProductId) });
        }
    }
}
