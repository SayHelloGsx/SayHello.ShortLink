using System;
using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Public.ShortLinks;

public class UpdateShortLinkDto : IHasConcurrencyStamp
{
    [Required]
    [StringLength(ShortLinkConsts.MaxTargetUrlLength)]
    public string TargetUrl { get; set; } = string.Empty;

    [StringLength(ShortLinkConsts.MaxTitleLength)]
    public string? Title { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [Required]
    [StringLength(40)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
