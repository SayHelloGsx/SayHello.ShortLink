using SayHello.ShortLink.WebHost.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace SayHello.ShortLink.WebHost.Web.Pages;

/* Inherit your PageModel classes from this class.
 */
public abstract class WebHostPageModel : AbpPageModel
{
    protected WebHostPageModel()
    {
        LocalizationResourceType = typeof(WebHostResource);
    }
}
