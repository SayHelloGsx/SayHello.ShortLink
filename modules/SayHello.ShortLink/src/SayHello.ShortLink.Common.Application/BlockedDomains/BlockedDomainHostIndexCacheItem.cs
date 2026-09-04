using System;
using System.Collections.Generic;
using Volo.Abp.Caching;

namespace SayHello.ShortLink.Common.BlockedDomains;

[Serializable]
[CacheName("BlockedDomainHostIndex")]
public class BlockedDomainHostIndexCacheItem
{
    public List<string> Hosts { get; set; } = [];
}
