using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.Common.BlockedDomains;

public interface IBlockedDomainCache
{
    Task<BlockedDomainResolutionCacheItem> GetAsync(
        string host,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        string domain,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task InvalidateManyAsync(
        IReadOnlyCollection<string> domains,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
