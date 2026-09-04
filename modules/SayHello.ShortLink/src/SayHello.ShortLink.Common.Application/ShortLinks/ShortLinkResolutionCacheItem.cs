using System;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Caching;

namespace SayHello.ShortLink.Common.ShortLinks;

[Serializable]
[CacheName("ShortLinkResolution")]
public class ShortLinkResolutionCacheItem
{
    public bool Exists { get; set; }

    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public string? TargetUrl { get; set; }

    public ShortLinkStatus Status { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsDeleted { get; set; }
}
