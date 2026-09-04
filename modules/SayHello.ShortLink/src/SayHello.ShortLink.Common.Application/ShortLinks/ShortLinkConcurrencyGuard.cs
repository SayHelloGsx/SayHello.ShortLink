using System;
using Volo.Abp.Data;
using ShortLinkEntity = SayHello.ShortLink.ShortLinks.ShortLink;

namespace SayHello.ShortLink.Common.ShortLinks;

public static class ShortLinkConcurrencyGuard
{
    public static void EnsureMatches(
        ShortLinkEntity shortLink,
        string concurrencyStamp)
    {
        if (!string.Equals(shortLink.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
        {
            throw new AbpDbConcurrencyException();
        }
    }
}
