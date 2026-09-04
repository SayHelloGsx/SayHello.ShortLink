using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkVisit : BasicAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ShortLinkId { get; protected set; }

    public DateTime VisitedAt { get; protected set; }

    public string VisitorHash { get; protected set; } = string.Empty;

    public string? ReferrerHost { get; protected set; }

    public string Browser { get; protected set; } = string.Empty;

    public string DeviceType { get; protected set; } = string.Empty;

    protected ShortLinkVisit()
    {
    }

    public ShortLinkVisit(
        Guid id,
        Guid? tenantId,
        Guid shortLinkId,
        DateTime visitedAt,
        string visitorHash,
        string? referrerHost,
        string browser,
        string deviceType)
        : base(id)
    {
        TenantId = tenantId;
        ShortLinkId = shortLinkId;
        VisitedAt = visitedAt.ToUniversalTime();
        VisitorHash = Check.NotNullOrWhiteSpace(
            visitorHash,
            nameof(visitorHash),
            ShortLinkConsts.VisitorHashLength);
        ReferrerHost = referrerHost.IsNullOrWhiteSpace()
            ? null
            : Check.Length(referrerHost!.Trim(), nameof(referrerHost), ShortLinkConsts.MaxHostLength);
        Browser = Check.NotNullOrWhiteSpace(
            browser,
            nameof(browser),
            ShortLinkConsts.MaxBrowserLength);
        DeviceType = Check.NotNullOrWhiteSpace(
            deviceType,
            nameof(deviceType),
            ShortLinkConsts.MaxDeviceTypeLength);
    }
}
