using System;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.ShortLinks;

public class GetShortLinksInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }

    public ShortLinkStatus? Status { get; set; }

    public Guid? OwnerUserId { get; set; }
}
