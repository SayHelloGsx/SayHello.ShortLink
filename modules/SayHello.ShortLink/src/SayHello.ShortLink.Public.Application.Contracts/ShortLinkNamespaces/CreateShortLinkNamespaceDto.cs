using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;

namespace SayHello.ShortLink.Public.ShortLinkNamespaces;

public class CreateShortLinkNamespaceDto
{
    [Required]
    [StringLength(ShortLinkDomainConsts.MaxOriginLength)]
    public string Origin { get; set; } = string.Empty;

    [Required]
    [StringLength(ShortLinkNamespaceConsts.MaxNameLength, MinimumLength = ShortLinkNamespaceConsts.MinNameLength)]
    [RegularExpression(@"\A[A-Za-z0-9_-]+\z")]
    public string Name { get; set; } = string.Empty;
}
