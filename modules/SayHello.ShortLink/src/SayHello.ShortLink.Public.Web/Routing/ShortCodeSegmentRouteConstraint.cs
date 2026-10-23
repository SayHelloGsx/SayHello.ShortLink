using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.Web.Routing;

public class ShortCodeSegmentRouteConstraint : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection) =>
        values.TryGetValue(routeKey, out var value) && ShortCodeFormat.IsValid(value?.ToString());
}
