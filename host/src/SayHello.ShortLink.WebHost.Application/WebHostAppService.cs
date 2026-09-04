using System;
using System.Collections.Generic;
using System.Text;
using SayHello.ShortLink.WebHost.Localization;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.WebHost;

/* Inherit your application services from this class.
 */
public abstract class WebHostAppService : ApplicationService
{
    protected WebHostAppService()
    {
        LocalizationResource = typeof(WebHostResource);
    }
}
