using SayHello.ShortLink.WebHost.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.ShortLink.WebHost.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class WebHostController : AbpControllerBase
{
    protected WebHostController()
    {
        LocalizationResource = typeof(WebHostResource);
    }
}
