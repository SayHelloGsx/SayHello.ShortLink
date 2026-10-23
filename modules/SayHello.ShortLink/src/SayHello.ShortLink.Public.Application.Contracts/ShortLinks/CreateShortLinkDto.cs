using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.ShortLinks;

public class CreateShortLinkDto : IValidatableObject
{
    [Required]
    [StringLength(ShortLinkDomainConsts.MaxOriginLength)]
    public string Origin { get; set; } = string.Empty;

    [Required]
    [StringLength(ShortLinkConsts.MaxTargetUrlLength)]
    public string TargetUrl { get; set; } = string.Empty;

    [StringLength(ShortLinkConsts.MaxCodeLength, MinimumLength = ShortLinkConsts.MinCodeLength)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string? CustomCode { get; set; }

    [StringLength(ShortLinkConsts.MaxTitleLength)]
    public string? Title { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public Guid? NamespaceId { get; set; }

    public bool UseRootPath { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NamespaceId.HasValue && UseRootPath)
        {
            yield return new ValidationResult(
                "A namespace and the root path cannot both be selected.",
                [nameof(NamespaceId), nameof(UseRootPath)]);
        }
        if (NamespaceId == Guid.Empty)
        {
            yield return new ValidationResult(
                "The namespace ID must not be empty.", [nameof(NamespaceId)]);
        }
    }
}
