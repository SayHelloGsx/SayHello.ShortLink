using SayHello.ShortLink.ShortLinkDomains;

namespace SayHello.ShortLink.Common.ShortLinks;

public static class ShortLinkResolutionCacheKey
{
    public static string Create(string origin, string code) =>
        Create(origin, string.Empty, code);

    public static string Create(string origin, string pathNamespace, string code) =>
        $"v2|{ShortLinkDomainOrigin.Normalize(origin)}|{pathNamespace}|{code}";
}
