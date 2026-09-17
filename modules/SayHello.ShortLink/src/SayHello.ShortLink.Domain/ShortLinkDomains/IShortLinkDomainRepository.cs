using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.ShortLink.ShortLinkDomains;

public interface IShortLinkDomainRepository : IRepository<ShortLinkDomain, Guid>
{
    Task<List<ShortLinkDomain>> GetListAsync(
        CancellationToken cancellationToken = default);

    Task<ShortLinkDomain?> FindByOriginAsync(
        string normalizedOrigin,
        bool enabledOnly = false,
        CancellationToken cancellationToken = default);

    Task<ShortLinkDomain?> FindDefaultAsync(
        CancellationToken cancellationToken = default);

    Task<bool> OriginExistsAsync(
        string normalizedOrigin,
        bool enabledOnly = false,
        CancellationToken cancellationToken = default);

    Task<bool> IsHostConfiguredAsync(
        string normalizedHost,
        CancellationToken cancellationToken = default);

    Task<long> GetShortLinkReferenceCountAsync(
        Guid domainId,
        CancellationToken cancellationToken = default);
}
