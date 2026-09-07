using SayHello.ShortLink.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.ShortLink;

public abstract class ShortLinkControllerBase : AbpControllerBase
{
    protected ShortLinkControllerBase()
    {
        LocalizationResource = typeof(ShortLinkResource);
    }
}
