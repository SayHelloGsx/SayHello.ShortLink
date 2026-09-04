using System;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.Common.ShortLinks;

public class VisitMetadataParser : IVisitMetadataParser, ITransientDependency
{
    public VisitMetadata Parse(string? referrer, string? userAgent)
    {
        return new VisitMetadata(
            ParseReferrerHost(referrer),
            ParseBrowser(userAgent),
            ParseDeviceType(userAgent));
    }

    private static string? ParseReferrerHost(string? referrer)
    {
        if (!Uri.TryCreate(referrer, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            return DomainNameNormalizer.Normalize(uri.IdnHost);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string ParseBrowser(string? userAgent)
    {
        if (userAgent.IsNullOrWhiteSpace())
        {
            return "Unknown";
        }

        if (Contains(userAgent, "Edg/"))
        {
            return "Edge";
        }

        if (Contains(userAgent, "OPR/") || Contains(userAgent, "Opera"))
        {
            return "Opera";
        }

        if (Contains(userAgent, "Chrome/") || Contains(userAgent, "CriOS/"))
        {
            return "Chrome";
        }

        if (Contains(userAgent, "Firefox/") || Contains(userAgent, "FxiOS/"))
        {
            return "Firefox";
        }

        if (Contains(userAgent, "Safari/"))
        {
            return "Safari";
        }

        return "Other";
    }

    private static string ParseDeviceType(string? userAgent)
    {
        if (userAgent.IsNullOrWhiteSpace())
        {
            return "Unknown";
        }

        if (Contains(userAgent, "bot") ||
            Contains(userAgent, "crawler") ||
            Contains(userAgent, "spider"))
        {
            return "Bot";
        }

        if (Contains(userAgent, "iPad") ||
            Contains(userAgent, "Tablet"))
        {
            return "Tablet";
        }

        if (Contains(userAgent, "Mobile") ||
            Contains(userAgent, "Android") ||
            Contains(userAgent, "iPhone"))
        {
            return "Mobile";
        }

        return "Desktop";
    }

    private static bool Contains(string value, string candidate)
    {
        return value.Contains(candidate, StringComparison.OrdinalIgnoreCase);
    }
}
