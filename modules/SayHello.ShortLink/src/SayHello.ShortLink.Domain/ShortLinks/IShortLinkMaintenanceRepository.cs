using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.ShortLinks;

public interface IShortLinkMaintenanceRepository
{
    Task ArchiveVisitsBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default);

    Task PurgeDeletedLinksBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default);
}
