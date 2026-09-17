using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.Common.BlockedDomains;

public interface IBlockedDomainCache
{
    Task<BlockedDomainResolutionCacheItem> GetAsync(
        string host,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        string domain,
        CancellationToken cancellationToken = default);

    Task InvalidateManyAsync(
        IReadOnlyCollection<string> domains,
        CancellationToken cancellationToken = default);
}
