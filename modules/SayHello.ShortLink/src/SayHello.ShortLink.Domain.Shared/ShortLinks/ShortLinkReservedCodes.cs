using System;
using System.Collections.Generic;

namespace SayHello.ShortLink.ShortLinks;

public static class ShortLinkReservedCodes
{
    private static readonly HashSet<string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        "abp",
        "account",
        "administration",
        "api",
        "connect",
        "error",
        "health",
        "libs",
        "short-links",
        "signin-oidc",
        "swagger",
        "themes"
    };

    public static bool Contains(string code)
    {
        return Values.Contains(code);
    }
}
