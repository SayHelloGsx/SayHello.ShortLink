using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.BlockedDomains;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public class UpdateBlockedDomainDto : IHasConcurrencyStamp
{
    [StringLength(BlockedDomainConsts.MaxReasonLength)]
    public string? Reason { get; set; }

    public bool IsActive { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
