using System;
using System.Collections.Generic;

namespace SayHello.ShortLink.ShortLinks;

public sealed class ShortLinkStatisticsData
{
    public long UniqueVisitorCount { get; }

    public IReadOnlyList<ShortLinkDailyVisitData> Daily { get; }

    public IReadOnlyList<ShortLinkDimensionVisitData> Referrers { get; }

    public IReadOnlyList<ShortLinkDimensionVisitData> Browsers { get; }

    public IReadOnlyList<ShortLinkDimensionVisitData> Devices { get; }

    public ShortLinkStatisticsData(
        long uniqueVisitorCount,
        IReadOnlyList<ShortLinkDailyVisitData> daily,
        IReadOnlyList<ShortLinkDimensionVisitData> referrers,
        IReadOnlyList<ShortLinkDimensionVisitData> browsers,
        IReadOnlyList<ShortLinkDimensionVisitData> devices)
    {
        UniqueVisitorCount = uniqueVisitorCount;
        Daily = daily;
        Referrers = referrers;
        Browsers = browsers;
        Devices = devices;
    }
}

public sealed record ShortLinkDailyVisitData(
    DateOnly Date,
    long VisitCount,
    long UniqueVisitorCount);

public sealed record ShortLinkDimensionVisitData(
    string Value,
    long VisitCount);
