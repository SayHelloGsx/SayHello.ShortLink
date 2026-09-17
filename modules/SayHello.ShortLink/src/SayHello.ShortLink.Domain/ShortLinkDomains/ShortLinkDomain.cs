using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomain : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public string Origin { get; protected set; } = string.Empty;

    public bool IsEnabled { get; protected set; }

    public bool IsDefault { get; protected set; }

    public string TenantScopeKey { get; protected set; } = string.Empty;

    public string DefaultUniquenessKey { get; protected set; } = string.Empty;

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

    public void Enable()
    {
        IsEnabled = true;
    }

    public void Disable()
    {
        if (IsDefault)
        {
            throw new BusinessException(ShortLinkErrorCodes.DefaultDomainCannotBeDisabled);
        }

        IsEnabled = false;
    }

    public void EnsureCanDelete()
    {
        if (IsDefault)
        {
            throw new BusinessException(ShortLinkErrorCodes.DefaultDomainCannotBeDeleted);
        }
    }

    internal void SetAsDefault()
    {
        IsEnabled = true;
        IsDefault = true;
        RefreshDefaultUniquenessKey();
    }

    internal void ClearDefault()
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
