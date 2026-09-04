using SayHello.ShortLink.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;

public abstract class ShortLinkAdminPageModel : AbpPageModel
{
    protected ShortLinkAdminPageModel()
    {
        LocalizationResourceType = typeof(ShortLinkResource);
        ObjectMapperContext = typeof(ShortLinkAdminWebModule);
    }
}
