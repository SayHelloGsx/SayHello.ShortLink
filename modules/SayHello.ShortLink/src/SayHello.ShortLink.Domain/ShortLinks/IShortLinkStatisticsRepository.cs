using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.ShortLinks;

public interface IShortLinkStatisticsRepository
{
    Task<ShortLinkStatisticsData> GetAsync(
        Guid shortLinkId,
        DateOnly startDate,
        DateOnly endDate,
        int maxDimensionItems,
        CancellationToken cancellationToken = default);
}
