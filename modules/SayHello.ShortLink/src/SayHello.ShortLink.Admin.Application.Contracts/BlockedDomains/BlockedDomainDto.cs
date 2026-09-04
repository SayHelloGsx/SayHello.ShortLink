using System;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public class BlockedDomainDto : FullAuditedEntityDto<Guid>
{
    public string Domain { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
