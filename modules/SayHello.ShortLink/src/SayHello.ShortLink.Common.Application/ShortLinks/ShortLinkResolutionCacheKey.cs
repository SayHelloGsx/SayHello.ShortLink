using SayHello.ShortLink.ShortLinkDomains;

namespace SayHello.ShortLink.Common.ShortLinks;

public static class ShortLinkResolutionCacheKey
{
    public static string Create(string origin, string code) =>
        $"{ShortLinkDomainOrigin.Normalize(origin)}|{code}";
}
