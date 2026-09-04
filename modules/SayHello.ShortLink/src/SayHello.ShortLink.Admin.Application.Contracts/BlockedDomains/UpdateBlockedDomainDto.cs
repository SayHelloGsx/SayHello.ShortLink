using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.BlockedDomains;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public class UpdateBlockedDomainDto
{
    [StringLength(BlockedDomainConsts.MaxReasonLength)]
    public string? Reason { get; set; }

    public bool IsActive { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
