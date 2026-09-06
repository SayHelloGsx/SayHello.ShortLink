using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace SayHello.Subscription;

public class SubscriptionPagedInput : PagedResultRequestDto, IValidatableObject
{
    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SkipCount < 0 || MaxResultCount < 1 || MaxResultCount > SubscriptionConsts.MaxPageSize)
        {
            yield return new ValidationResult($"Page size must be between 1 and {SubscriptionConsts.MaxPageSize}.",
                new[] { nameof(SkipCount), nameof(MaxResultCount) });
        }
    }
}
