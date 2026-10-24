using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.ShortLinks;

public class SetShortLinkStatusDto : IHasConcurrencyStamp
{
    [Required]
    public ShortLinkStatus Status { get; set; }

    [Required]
    [StringLength(40)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
