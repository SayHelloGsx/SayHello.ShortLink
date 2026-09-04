using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public class CreateBlockedDomainDto
{
    [Required]
    [StringLength(ShortLinkConsts.MaxHostLength)]
    public string Domain { get; set; } = string.Empty;

    [StringLength(BlockedDomainConsts.MaxReasonLength)]
    public string? Reason { get; set; }
}
