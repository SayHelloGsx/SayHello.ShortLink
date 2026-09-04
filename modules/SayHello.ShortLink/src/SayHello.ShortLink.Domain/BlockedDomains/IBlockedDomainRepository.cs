using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.ShortLink.BlockedDomains;

public interface IBlockedDomainRepository : IRepository<BlockedDomain, Guid>
{
    Task<List<BlockedDomain>> GetListAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<BlockedDomain?> FindMatchingActiveAsync(
        string normalizedHost,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetExistingDomainsAsync(
        IReadOnlyCollection<string> normalizedDomains,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> IsBlockedAsync(
        string normalizedHost,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string normalizedDomain,
        Guid? tenantId,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);
}
