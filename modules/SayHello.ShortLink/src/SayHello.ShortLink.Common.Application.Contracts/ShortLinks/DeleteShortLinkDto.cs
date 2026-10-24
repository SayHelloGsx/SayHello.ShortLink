using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.ShortLinks;

public class DeleteShortLinkDto : IHasConcurrencyStamp
{
    [Required]
    [StringLength(40)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
