using System;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkDto : FullAuditedEntityDto<Guid>
{
    public Guid OwnerUserId { get; set; }

    public Guid? DomainId { get; set; }

    public string? Origin { get; set; }

    public string Code { get; set; } = string.Empty;

    public string ShortUrl { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public string? Title { get; set; }

    public ShortLinkStatus Status { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public long? TotalVisitCount { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
