using System;
using System.Globalization;

namespace SayHello.ShortLink.ShortLinks;

public static class DomainNameNormalizer
{
    public static string Normalize(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        var trimmedHost = host.Trim().TrimEnd('.');
        var asciiHost = new IdnMapping().GetAscii(trimmedHost);

        if (asciiHost.Length > ShortLinkConsts.MaxHostLength)
        {
            throw new ArgumentOutOfRangeException(nameof(host));
        }

        return asciiHost.ToLowerInvariant();
    }

    public static bool IsSameOrSubdomainOf(string host, string candidateParent)
    {
        return host.Equals(candidateParent, StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith("." + candidateParent, StringComparison.OrdinalIgnoreCase);
    }
}
