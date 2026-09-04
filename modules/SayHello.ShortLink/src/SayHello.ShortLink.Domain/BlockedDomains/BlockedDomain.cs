using System;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.BlockedDomains;

public class BlockedDomain : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public string Domain { get; protected set; } = string.Empty;

    public string? Reason { get; protected set; }

    public bool IsActive { get; protected set; }

    protected BlockedDomain()
    {
    }

    public BlockedDomain(
        Guid id,
        Guid? tenantId,
        string domain,
        string? reason = null)
        : base(id)
    {
        TenantId = tenantId;
        Domain = DomainNameNormalizer.Normalize(domain);
        IsActive = true;
        SetReason(reason);
    }

    public void Update(string? reason, bool isActive)
    {
        SetReason(reason);
        IsActive = isActive;
    }

    private void SetReason(string? reason)
    {
        Reason = reason.IsNullOrWhiteSpace()
            ? null
            : Check.Length(reason!.Trim(), nameof(reason), BlockedDomainConsts.MaxReasonLength);
    }
}
