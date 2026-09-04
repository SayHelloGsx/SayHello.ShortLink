using System;
using Volo.Abp.Caching;

namespace SayHello.ShortLink.Common.BlockedDomains;

[Serializable]
[CacheName("BlockedDomainResolution")]
public class BlockedDomainResolutionCacheItem
{
    public bool IsBlocked { get; set; }

    public string? MatchedDomain { get; set; }

    public string? Reason { get; set; }
}
