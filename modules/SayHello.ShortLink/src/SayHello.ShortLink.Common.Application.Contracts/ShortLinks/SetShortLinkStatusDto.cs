using System.ComponentModel.DataAnnotations;

namespace SayHello.ShortLink.ShortLinks;

public class SetShortLinkStatusDto
{
    [Required]
    public ShortLinkStatus Status { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
