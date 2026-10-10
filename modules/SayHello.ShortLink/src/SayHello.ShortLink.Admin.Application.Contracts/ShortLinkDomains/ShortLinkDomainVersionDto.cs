using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Admin.ShortLinkDomains;

public class ShortLinkDomainVersionDto : IHasConcurrencyStamp
{
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
