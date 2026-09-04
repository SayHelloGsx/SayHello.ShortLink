using System;
using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.ShortLinks;

public class CreateShortLinkDto
{
    [Required]
    [StringLength(ShortLinkConsts.MaxTargetUrlLength)]
    public string TargetUrl { get; set; } = string.Empty;

    [StringLength(ShortLinkConsts.MaxCodeLength, MinimumLength = ShortLinkConsts.MinCodeLength)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string? CustomCode { get; set; }

    [StringLength(ShortLinkConsts.MaxTitleLength)]
    public string? Title { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
