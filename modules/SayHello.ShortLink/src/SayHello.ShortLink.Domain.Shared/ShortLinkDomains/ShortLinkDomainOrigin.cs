using System;
using System.Globalization;
using System.Net;
using Volo.Abp;

namespace SayHello.ShortLink.ShortLinkDomains;

public static class ShortLinkDomainOrigin
{
    public static string Normalize(string origin)
    {
        if (origin.IsNullOrWhiteSpace())
        {
            throw InvalidOrigin();
        }

        var candidate = origin.Trim();
        if (candidate.Length > ShortLinkDomainConsts.MaxOriginLength ||
            candidate.Contains('?', StringComparison.Ordinal) ||
            candidate.Contains('#', StringComparison.Ordinal) ||
            !HasOnlyOptionalTrailingSlash(candidate) ||
            !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !uri.UserInfo.IsNullOrEmpty() ||
            uri.Host.IsNullOrWhiteSpace() ||
            uri.AbsolutePath != "/")
        {
            throw InvalidOrigin();
        }

        string normalizedHost;
        try
        {
            normalizedHost = NormalizeHost(uri);
        }
        catch (ArgumentException)
        {
            throw InvalidOrigin();
        }

        if (normalizedHost.IsNullOrWhiteSpace())
        {
            throw InvalidOrigin();
        }

        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{normalizedHost}]"
            : normalizedHost;
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        var normalized = $"{uri.Scheme.ToLowerInvariant()}://{host}{port}";

        if (normalized.Length > ShortLinkDomainConsts.MaxOriginLength)
        {
            throw InvalidOrigin();
        }

        return normalized;
    }

    public static string FromUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw InvalidOrigin();
        }

        var builder = new UriBuilder(uri)
        {
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };
        return Normalize(builder.Uri.GetLeftPart(UriPartial.Authority));
    }

    private static string NormalizeHost(Uri uri)
    {
        var host = uri.Host.Trim('[', ']');
        if (IPAddress.TryParse(host, out var address))
        {
            return address.ToString().ToLowerInvariant();
        }

        return new IdnMapping()
            .GetAscii(uri.IdnHost.TrimEnd('.'))
            .ToLowerInvariant();
    }

    private static bool HasOnlyOptionalTrailingSlash(string candidate)
    {
        var schemeSeparator = candidate.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator <= 0)
        {
            return false;
        }

        var authorityStart = schemeSeparator + 3;
        var suffixStart = candidate.IndexOfAny(
            ['/', '\\', '?', '#'],
            authorityStart);
        var authorityEnd = suffixStart < 0 ? candidate.Length : suffixStart;
        if (candidate.AsSpan(authorityStart, authorityEnd - authorityStart)
            .Contains('@'))
        {
            return false;
        }

        return suffixStart < 0 || candidate[suffixStart..] == "/";
    }

    private static BusinessException InvalidOrigin()
    {
        return new BusinessException(ShortLinkErrorCodes.InvalidDomainOrigin);
    }
}
