using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.ShortLinks;

public interface IShortLinkCapabilityProvider
{
    bool IsQuotaExternallyManaged { get; }

    Task<ShortLinkQuota> GetQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ShortLinkStatisticsLevel> GetStatisticsLevelAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ShortLinkDomainAccess> GetDomainAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
