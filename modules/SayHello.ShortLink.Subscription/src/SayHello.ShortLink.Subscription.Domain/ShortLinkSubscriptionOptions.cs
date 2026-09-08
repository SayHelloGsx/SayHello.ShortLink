namespace SayHello.ShortLink.Subscription;

public sealed class ShortLinkSubscriptionOptions
{
    public string ProductCode { get; set; } = string.Empty;
    public string QuotaFeatureKey { get; set; } = string.Empty;
    public string StatisticsFeatureKey { get; set; } = string.Empty;
}
