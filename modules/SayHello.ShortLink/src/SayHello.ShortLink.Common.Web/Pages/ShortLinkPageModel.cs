using SayHello.ShortLink.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace SayHello.ShortLink.Web.Pages;

public abstract class ShortLinkPageModel : AbpPageModel
{
    protected ShortLinkPageModel()
    {
        LocalizationResourceType = typeof(ShortLinkResource);
    }
}
