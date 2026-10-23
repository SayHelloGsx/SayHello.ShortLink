using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.ShortLinkNamespaces;

namespace SayHello.ShortLink.Public.Web.Routing;

public class ShortLinkNamespaceRouteConstraint(IOptions<ShortLinkNamespaceRouteOptions> options) : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection) =>
        values.TryGetValue(routeKey, out var value) &&
        value?.ToString() is { } name &&
        ShortLinkNamespaceFormat.IsValid(name) &&
        !options.Value.ReservedPrefixes.Contains(name);
}
