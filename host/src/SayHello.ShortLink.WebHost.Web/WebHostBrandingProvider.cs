using Microsoft.Extensions.Localization;
using SayHello.ShortLink.WebHost.Localization;
using Volo.Abp.Ui.Branding;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.WebHost.Web;

[Dependency(ReplaceServices = true)]
public class WebHostBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<WebHostResource> _localizer;

    public WebHostBrandingProvider(IStringLocalizer<WebHostResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
