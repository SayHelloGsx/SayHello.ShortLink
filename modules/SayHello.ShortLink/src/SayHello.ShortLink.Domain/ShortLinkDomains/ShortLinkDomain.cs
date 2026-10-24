using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomain : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    public virtual string Origin { get; protected set; } = string.Empty;

    public virtual bool IsEnabled { get; protected set; }

    public virtual bool IsDefault { get; protected set; }

    public virtual string TenantScopeKey { get; protected set; } = string.Empty;

    public virtual string DefaultUniquenessKey { get; protected set; } = string.Empty;

    protected ShortLinkDomain()
    {
    }

    public ShortLinkDomain(
        Guid id,
        Guid? tenantId,
        string origin,
        bool isDefault = false)
        : base(EnsureId(id))
    {
        TenantId = tenantId;
        TenantScopeKey = GetTenantScopeKey(tenantId);
        Origin = ShortLinkDomainOrigin.Normalize(origin);
        IsEnabled = true;
        IsDefault = isDefault;
        RefreshDefaultUniquenessKey();
    }

    public virtual void Enable()
    {
        IsEnabled = true;
    }

    public virtual void Disable()
    {
        if (IsDefault)
        {
            throw new BusinessException(ShortLinkErrorCodes.DefaultDomainCannotBeDisabled);
        }

        IsEnabled = false;
    }

    public virtual void EnsureCanDelete()
    {
        if (IsDefault)
        {
            throw new BusinessException(ShortLinkErrorCodes.DefaultDomainCannotBeDeleted);
        }
    }

    internal virtual void SetAsDefault()
    {
        IsEnabled = true;
        IsDefault = true;
        RefreshDefaultUniquenessKey();
    }

    internal virtual void ClearDefault()
    {
        IsDefault = false;
        RefreshDefaultUniquenessKey();
    }

    private void RefreshDefaultUniquenessKey()
    {
        DefaultUniquenessKey = IsDefault
            ? $"default:{TenantScopeKey}"
            : $"domain:{Id:N}";
    }

    private static Guid EnsureId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A short-link domain ID is required.", nameof(id));
        }

        return id;
    }

    private static string GetTenantScopeKey(Guid? tenantId)
    {
        return tenantId?.ToString("N") ?? "host";
    }
}
