using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;

namespace SayHello.ShortLink.BlockedDomains;

/// <summary>
/// A blocked domain entry is a DNS host name or an IP address. IP addresses are stored in
/// their canonical text form (for example <c>[2001:DB8::1]</c> becomes <c>2001:db8::1</c>)
/// so that they match the normalized host of a target URL.
/// </summary>
public static class BlockedDomainName
{
    public static bool TryNormalize(
        string? domain,
        [NotNullWhen(true)] out string? normalizedDomain)
    {
        normalizedDomain = null;
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        string host;
        try
        {
            host = DomainNameNormalizer.Normalize(domain);
        }
        catch (ArgumentException)
        {
            return false;
        }

        // Uri.CheckHostName accepts the same IPv4 notations as target URLs, such as 0xCB.0.113.5.
        switch (Uri.CheckHostName(host))
        {
            case UriHostNameType.Dns:
                normalizedDomain = host;
                return true;
            case UriHostNameType.IPv4:
            case UriHostNameType.IPv6:
                var address = host.StartsWith('[') && host.EndsWith(']')
                    ? host[1..^1]
                    : host;
                if (address.Contains('%') ||
                    !IPAddress.TryParse(address, out var ipAddress))
                {
                    return false;
                }

                normalizedDomain = ipAddress.ToString();
                return true;
            default:
                return false;
        }
    }

    public static string Normalize(string? domain)
    {
        if (!TryNormalize(domain, out var normalizedDomain))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidBlockedDomain)
                .WithData("Domain", domain?.Trim() ?? string.Empty);
        }

        return normalizedDomain;
    }
}
