using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.ShortLink.ShortLinks;

public interface IShortLinkRepository : IRepository<ShortLink, Guid>
{
    Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<ShortLink?> FindByCodeAsync(
        string code,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    Task<long> GetCountByOwnerAsync(
        Guid ownerUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<List<ShortLink>> GetListAsync(
        Guid? ownerUserId,
        Guid? tenantId,
        string? filter,
        ShortLinkStatus? status,
        string? sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(
        Guid? ownerUserId,
        Guid? tenantId,
        string? filter,
        ShortLinkStatus? status,
        CancellationToken cancellationToken = default);

    Task RecordVisitAsync(
        ShortLinkVisit visit,
        CancellationToken cancellationToken = default);
}
