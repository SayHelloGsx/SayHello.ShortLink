using System;
using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.ShortLinks;

public class UpdateShortLinkDto
{
    [Required]
    [StringLength(ShortLinkConsts.MaxTargetUrlLength)]
    public string TargetUrl { get; set; } = string.Empty;

    [StringLength(ShortLinkConsts.MaxTitleLength)]
    public string? Title { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
