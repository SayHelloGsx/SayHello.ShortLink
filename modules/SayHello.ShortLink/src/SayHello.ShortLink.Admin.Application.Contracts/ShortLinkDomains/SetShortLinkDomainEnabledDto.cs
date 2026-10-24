using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Admin.ShortLinkDomains;

public class SetShortLinkDomainEnabledDto : IHasConcurrencyStamp
{
    public bool IsEnabled { get; set; }

    [Required]
    [StringLength(40)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
