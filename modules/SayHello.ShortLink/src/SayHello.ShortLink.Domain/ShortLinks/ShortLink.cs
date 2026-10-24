using System;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLink : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    public virtual Guid OwnerUserId { get; protected set; }

    public virtual Guid? DomainId { get; protected set; }

    public virtual string? Origin { get; protected set; }

    public virtual string Namespace { get; protected set; } = string.Empty;

    public virtual Guid? NamespaceId { get; protected set; }

    public virtual string Code { get; protected set; } = string.Empty;

    public virtual bool IsCustomCode { get; protected set; }

    public virtual string TargetUrl { get; protected set; } = string.Empty;

    public virtual string? Title { get; protected set; }

    public virtual ShortLinkStatus Status { get; protected set; }

    public virtual DateTime? ExpiresAt { get; protected set; }

    public virtual long TotalVisitCount { get; protected set; }

    protected ShortLink()
    {
    }

    internal ShortLink(
        Guid id,
        Guid? tenantId,
        Guid ownerUserId,
        Guid domainId,
        string origin,
        string code,
        string targetUrl,
        string? title,
        DateTime? expiresAt,
        bool isCustomCode = false,
        string pathNamespace = "",
        Guid? namespaceId = null)
        : base(id)
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        DomainId = domainId == Guid.Empty
            ? throw new ArgumentException("A short-link domain ID is required.", nameof(domainId))
            : domainId;
        Origin = ShortLinkDomainOrigin.Normalize(origin);
        if (pathNamespace != string.Empty && !ShortLinkNamespaceFormat.IsValid(pathNamespace))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidNamespace);
        }
        if (namespaceId.HasValue && (namespaceId == Guid.Empty || pathNamespace.Length == 0))
        {
            throw new BusinessException(ShortLinkErrorCodes.NamespaceSelectionConflict);
        }
        Namespace = pathNamespace;
        NamespaceId = namespaceId;
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), ShortLinkConsts.MaxCodeLength);
        IsCustomCode = isCustomCode;
        Status = ShortLinkStatus.Active;
        TotalVisitCount = 0;

        SetTarget(targetUrl);
        SetTitle(title);
        SetExpiration(expiresAt);
    }

    internal ShortLink(
        Guid id,
        Guid? tenantId,
        Guid ownerUserId,
        string code,
        string targetUrl,
        string? title,
        DateTime? expiresAt,
        bool isCustomCode = false)
        : base(id)
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), ShortLinkConsts.MaxCodeLength);
        IsCustomCode = isCustomCode;
        Status = ShortLinkStatus.Active;
        TotalVisitCount = 0;

        SetTarget(targetUrl);
        SetTitle(title);
        SetExpiration(expiresAt);
    }

    internal virtual void AssignDomain(Guid domainId, string origin)
    {
        if (DomainId.HasValue || !Origin.IsNullOrWhiteSpace())
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidState);
        }

        DomainId = domainId == Guid.Empty
            ? throw new ArgumentException("A short-link domain ID is required.", nameof(domainId))
            : domainId;
        Origin = ShortLinkDomainOrigin.Normalize(origin);
    }

    public virtual void Update(string targetUrl, string? title, DateTime? expiresAt)
    {
        SetTarget(targetUrl);
        SetTitle(title);
        SetExpiration(expiresAt);
    }

    public virtual void Activate()
    {
        Status = ShortLinkStatus.Active;
    }

    public virtual void Disable()
    {
        Status = ShortLinkStatus.Disabled;
    }

    public virtual bool IsExpired(DateTime now)
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= now;
    }

    public virtual void IncreaseVisitCount()
    {
        checked
        {
            TotalVisitCount++;
        }
    }

    private void SetTarget(string targetUrl)
    {
        TargetUrl = Check.NotNullOrWhiteSpace(
            targetUrl,
            nameof(targetUrl),
            ShortLinkConsts.MaxTargetUrlLength);
    }

    private void SetTitle(string? title)
    {
        Title = title.IsNullOrWhiteSpace()
            ? null
            : Check.Length(title!.Trim(), nameof(title), ShortLinkConsts.MaxTitleLength);
    }

    private static void ValidateExpiration(DateTime? expiresAt)
    {
        if (expiresAt.HasValue && expiresAt.Value.Kind == DateTimeKind.Unspecified)
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidState);
        }
    }

    private void SetExpiration(DateTime? expiresAt)
    {
        ValidateExpiration(expiresAt);
        ExpiresAt = expiresAt?.ToUniversalTime();
    }
}
