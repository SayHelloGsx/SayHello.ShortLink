using System;
using SayHello.ShortLink.ShortLinkDomains;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class ShortLinkNamespace : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public Guid OwnerUserId { get; protected set; }
    public Guid DomainId { get; protected set; }
    public string Origin { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;

    protected ShortLinkNamespace()
    {
    }

    internal ShortLinkNamespace(Guid id, Guid? tenantId, Guid ownerUserId, Guid domainId, string origin, string name)
        : base(id)
    {
        if (ownerUserId == Guid.Empty || domainId == Guid.Empty)
        {
            throw new BusinessException(ShortLinkErrorCodes.NamespaceAccessDenied);
        }

        if (!ShortLinkNamespaceFormat.IsValid(name))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidNamespace);
        }

        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        DomainId = domainId;
        Origin = ShortLinkDomainOrigin.Normalize(origin);
        Name = name;
    }
}
