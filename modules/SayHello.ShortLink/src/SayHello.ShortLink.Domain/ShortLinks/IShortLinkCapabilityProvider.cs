using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.ShortLinks;

public interface IShortLinkCapabilityProvider
{
    bool IsQuotaExternallyManaged { get; }

    Task<ShortLinkQuota> GetQuotaAsync(
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsStatisticsEnabledAsync(
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
