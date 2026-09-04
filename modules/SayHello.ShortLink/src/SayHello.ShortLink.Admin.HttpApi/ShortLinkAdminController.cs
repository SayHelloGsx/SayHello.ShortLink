using SayHello.ShortLink.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.ShortLink.Admin;

public abstract class ShortLinkAdminController : AbpControllerBase
{
    protected ShortLinkAdminController()
    {
        LocalizationResource = typeof(ShortLinkResource);
    }
}
