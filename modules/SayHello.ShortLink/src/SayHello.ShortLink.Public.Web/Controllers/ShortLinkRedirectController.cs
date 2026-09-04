using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Public.ShortLinks;

namespace SayHello.ShortLink.Public.Web.Controllers;

[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class ShortLinkRedirectController : Controller
{
    private readonly IShortLinkRedirectAppService _redirectAppService;

    public ShortLinkRedirectController(IShortLinkRedirectAppService redirectAppService)
    {
        _redirectAppService = redirectAppService;
    }

    [AcceptVerbs("GET", "HEAD")]
    [Route("{code:shortCode}", Order = 1000)]
    public async Task<IActionResult> ResolveAsync(string code)
    {
        var isHead = HttpContext.Request.Method == HttpMethods.Head;
        var visit = isHead
            ? null
            : new RecordShortLinkVisitDto
            {
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Referrer = Request.Headers.Referer.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            };

        var result = await _redirectAppService.ResolveAsync(code, visit);
        Response.Headers.CacheControl = "no-store";

        if (result.Status == ShortLinkResolutionStatus.Found)
        {
            return Redirect(result.TargetUrl!);
        }

        if (result.Status == ShortLinkResolutionStatus.Gone)
        {
            Response.StatusCode = StatusCodes.Status410Gone;
            return View("Gone", code);
        }

        if (result.Status == ShortLinkResolutionStatus.Blocked)
        {
            Response.StatusCode = StatusCodes.Status451UnavailableForLegalReasons;
            return View("Blocked", result);
        }

        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound", code);
    }
}
