using System;
using System.Collections.Generic;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class ShortLinkNamespaceRouteOptions
{
    public HashSet<string> ReservedPrefixes { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "abp", "account", "admin", "administration", "api", "connect", "error",
        "health", "identity", "libs", "short-links", "signin-oidc", "signout-oidc",
        "signout-callback-oidc", "swagger", "themes", "subscriptions",
        "tenantmanagement", "settingmanagement", "featuremanagement",
        "abppermissionmanagement", "css", "js", "images",
        "_content", "_framework"
    };
}
