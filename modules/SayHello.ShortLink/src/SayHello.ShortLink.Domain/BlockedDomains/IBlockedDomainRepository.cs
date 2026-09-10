using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.ShortLink.BlockedDomains;

public interface IBlockedDomainRepository : IRepository<BlockedDomain, Guid>
{
    Task<List<BlockedDomain>> GetListAsync(
        CancellationToken cancellationToken = default);

    Task<BlockedDomain?> FindMatchingActiveAsync(
        string normalizedHost,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetExistingDomainsAsync(
        IReadOnlyCollection<string> normalizedDomains,
        CancellationToken cancellationToken = default);

    Task<bool> IsBlockedAsync(
        string normalizedHost,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string normalizedDomain,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);
}
