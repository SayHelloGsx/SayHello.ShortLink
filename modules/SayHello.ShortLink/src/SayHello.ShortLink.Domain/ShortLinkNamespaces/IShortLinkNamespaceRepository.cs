using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public interface IShortLinkNamespaceRepository : IRepository<ShortLinkNamespace, Guid>
{
    Task<bool> NameExistsAsync(string origin, string name, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);
    Task<long> GetCountByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<List<ShortLinkNamespace>> GetListByOwnerAsync(
        Guid ownerUserId, string? origin = null, CancellationToken cancellationToken = default);
    Task<bool> HasLinkReferencesAsync(Guid namespaceId, CancellationToken cancellationToken = default);
    Task<bool> HasDomainReferencesAsync(Guid domainId, CancellationToken cancellationToken = default);
}
