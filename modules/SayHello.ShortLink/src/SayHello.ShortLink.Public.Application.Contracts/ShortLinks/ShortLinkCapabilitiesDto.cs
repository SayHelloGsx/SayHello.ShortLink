using System.Collections.Generic;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.ShortLinks;

public class ShortLinkCapabilitiesDto
{
    public long UsedLinks { get; set; }

    public bool IsQuotaGranted { get; set; }

    public bool IsUnlimited { get; set; }

    public long? MaxLinks { get; set; }

    public long? RemainingLinks { get; set; }

    public ShortLinkStatisticsLevel StatisticsLevel { get; set; }

    public bool StatisticsEnabled => StatisticsLevel != ShortLinkStatisticsLevel.None;

    public List<ShortLinkDomainOptionDto> Domains { get; set; } = [];
}

public class ShortLinkDomainOptionDto
{
    public string Origin { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
