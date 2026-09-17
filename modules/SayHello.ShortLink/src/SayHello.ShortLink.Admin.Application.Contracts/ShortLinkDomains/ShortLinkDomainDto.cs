using System;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.Admin.ShortLinkDomains;

public class ShortLinkDomainDto : AuditedEntityDto<Guid>
{
    public string Origin { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public bool IsDefault { get; set; }

    public long ShortLinkReferenceCount { get; set; }

    public bool CanDisable => IsEnabled && !IsDefault;

    public bool CanDelete => !IsDefault && ShortLinkReferenceCount == 0;
}
