using System;

namespace SayHello.ShortLink.ShortLinks;

public sealed class ShortLinkQuota
{
    public static ShortLinkQuota Denied { get; } = new(false, false, null);
    public static ShortLinkQuota Unlimited { get; } = new(true, true, null);

    public bool IsGranted { get; }
    public bool IsUnlimited { get; }
    public long? Limit { get; }

    private ShortLinkQuota(bool isGranted, bool isUnlimited, long? limit)
    {
        IsGranted = isGranted;
        IsUnlimited = isUnlimited;
        Limit = limit;
    }

    public static ShortLinkQuota Limited(long limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        return new ShortLinkQuota(true, false, limit);
    }

    public bool AllowsCreation(long currentCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentCount);
        return IsGranted && (IsUnlimited || currentCount < Limit);
    }
}
