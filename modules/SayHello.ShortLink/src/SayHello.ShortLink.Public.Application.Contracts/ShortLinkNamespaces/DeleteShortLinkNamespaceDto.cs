using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Public.ShortLinkNamespaces;

public class DeleteShortLinkNamespaceDto : IHasConcurrencyStamp
{
    [Required]
    [StringLength(40)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
