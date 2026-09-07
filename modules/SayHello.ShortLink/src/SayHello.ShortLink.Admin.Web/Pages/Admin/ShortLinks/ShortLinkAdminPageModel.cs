using SayHello.ShortLink.Web.Pages;

namespace SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;

public abstract class ShortLinkAdminPageModel : ShortLinkPageModel
{
    protected ShortLinkAdminPageModel()
    {
        ObjectMapperContext = typeof(ShortLinkAdminWebModule);
    }
}
