using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.ShortLinks;

public interface IShortLinkCapabilityProvider
{
    bool IsQuotaExternallyManaged { get; }

    Task<ShortLinkQuota> GetNamespaceQuotaAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ShortLinkQuota> GetRootLinkQuotaAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ShortLinkQuota> GetQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ShortLinkQuota> GetCustomCodeQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ShortLinkStatisticsLevel> GetStatisticsLevelAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ShortLinkDomainAccess> GetDomainAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
