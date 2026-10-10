using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Admin.ShortLinkDomains;

public class ShortLinkDomainDto : AuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Origin { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public bool IsDefault { get; set; }

    public long ShortLinkReferenceCount { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;

    public bool CanDisable => IsEnabled && !IsDefault;

    public bool CanDelete => !IsDefault && ShortLinkReferenceCount == 0;
}
