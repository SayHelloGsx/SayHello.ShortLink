using SayHello.ShortLink.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.ShortLink.Public;

public abstract class ShortLinkPublicController : AbpControllerBase
{
    protected ShortLinkPublicController()
    {
        LocalizationResource = typeof(ShortLinkResource);
    }
}
