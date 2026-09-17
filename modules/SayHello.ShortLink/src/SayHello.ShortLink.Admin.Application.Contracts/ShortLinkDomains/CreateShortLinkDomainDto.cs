using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.ShortLinkDomains;

namespace SayHello.ShortLink.Admin.ShortLinkDomains;

public class CreateShortLinkDomainDto
{
    [Required]
    [StringLength(ShortLinkDomainConsts.MaxOriginLength)]
    public string Origin { get; set; } = string.Empty;
}
