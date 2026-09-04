using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.Web.Routing;

public partial class ShortCodeRouteConstraint : IRouteConstraint
{
    [GeneratedRegex("^[A-Za-z0-9_-]{3,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        return values.TryGetValue(routeKey, out var value) &&
               value is not null &&
               value.ToString() is { } code &&
               CodeRegex().IsMatch(code) &&
               !ShortLinkReservedCodes.Contains(code);
    }
}
