using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SayHello.Subscription.Public.Catalog;

public class GetPublicCatalogInput : SubscriptionPagedInput
{
    [StringLength(SubscriptionConsts.MaxNameLength)]
    public string? Filter { get; set; }

    public Guid? ProductId { get; set; }

    [EnumDataType(typeof(SubscriptionCatalogSort))]
    public SubscriptionCatalogSort Sorting { get; set; } = SubscriptionCatalogSort.DisplayOrder;

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
