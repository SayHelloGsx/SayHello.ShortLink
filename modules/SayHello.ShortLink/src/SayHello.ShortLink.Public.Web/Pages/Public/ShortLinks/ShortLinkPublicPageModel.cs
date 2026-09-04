using SayHello.ShortLink.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

/* Inherit your PageModel classes from this class.
 */
public abstract class ShortLinkPublicPageModel : AbpPageModel
{
    protected ShortLinkPublicPageModel()
    {
        LocalizationResourceType = typeof(ShortLinkResource);
        ObjectMapperContext = typeof(ShortLinkPublicWebModule);
    }
}
