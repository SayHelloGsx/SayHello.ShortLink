namespace SayHello.ShortLink.Public.ShortLinks;

public class ShortLinkCapabilitiesDto
{
    public long UsedLinks { get; set; }

    public bool IsQuotaGranted { get; set; }

    public bool IsUnlimited { get; set; }

    public long? MaxLinks { get; set; }

    public long? RemainingLinks { get; set; }

    public bool StatisticsEnabled { get; set; }
}
