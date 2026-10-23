using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Public.ShortLinkNamespaces;

public class ShortLinkNamespaceDto : EntityDto<Guid>, IHasConcurrencyStamp
{
    public Guid DomainId { get; set; }

    public string Origin { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime CreationTime { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
