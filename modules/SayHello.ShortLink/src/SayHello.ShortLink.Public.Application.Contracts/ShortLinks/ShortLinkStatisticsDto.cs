using System;
using System.Collections.Generic;

namespace SayHello.ShortLink.Public.ShortLinks;

public class ShortLinkStatisticsDto
{
    public Guid ShortLinkId { get; set; }

    public string Code { get; set; } = string.Empty;

    public long TotalVisitCount { get; set; }

    public long UniqueVisitorCount { get; set; }

    public List<DailyVisitStatisticDto> Daily { get; set; } = [];

    public List<DimensionStatisticDto> Referrers { get; set; } = [];

    public List<DimensionStatisticDto> Browsers { get; set; } = [];

    public List<DimensionStatisticDto> Devices { get; set; } = [];
}

public class DailyVisitStatisticDto
{
    public DateOnly Date { get; set; }

    public long VisitCount { get; set; }

    public long UniqueVisitorCount { get; set; }
}

public class DimensionStatisticDto
{
    public string Value { get; set; } = string.Empty;

    public long VisitCount { get; set; }
}
