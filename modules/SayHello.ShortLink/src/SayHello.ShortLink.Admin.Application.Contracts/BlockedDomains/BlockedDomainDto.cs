using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public class BlockedDomainDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Domain { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
