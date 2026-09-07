using SayHello.ShortLink.Web.Pages;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

/* Inherit your PageModel classes from this class.
 */
public abstract class ShortLinkPublicPageModel : ShortLinkPageModel
{
    protected ShortLinkPublicPageModel()
    {
        ObjectMapperContext = typeof(ShortLinkPublicWebModule);
    }
}
