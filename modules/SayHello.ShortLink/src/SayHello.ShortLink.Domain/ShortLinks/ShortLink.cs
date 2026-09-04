using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLink : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid OwnerUserId { get; protected set; }

    public string Code { get; protected set; } = string.Empty;

    public string TargetUrl { get; protected set; } = string.Empty;

    public string? Title { get; protected set; }

    public ShortLinkStatus Status { get; protected set; }

    public DateTime? ExpiresAt { get; protected set; }

    public long TotalVisitCount { get; protected set; }

    protected ShortLink()
    {
    }

    internal ShortLink(
        Guid id,
        Guid? tenantId,
        Guid ownerUserId,
        string code,
        string targetUrl,
        string? title,
        DateTime? expiresAt)
        : base(id)
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), ShortLinkConsts.MaxCodeLength);
        Status = ShortLinkStatus.Active;
        TotalVisitCount = 0;

        SetTarget(targetUrl);
        SetTitle(title);
        SetExpiration(expiresAt);
    }

    public void Update(string targetUrl, string? title, DateTime? expiresAt)
    {
        SetTarget(targetUrl);
        SetTitle(title);
        SetExpiration(expiresAt);
    }

    public void Activate()
    {
        Status = ShortLinkStatus.Active;
    }

    public void Disable()
    {
        Status = ShortLinkStatus.Disabled;
    }

    public bool IsExpired(DateTime now)
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= now;
    }

    public void IncreaseVisitCount()
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
