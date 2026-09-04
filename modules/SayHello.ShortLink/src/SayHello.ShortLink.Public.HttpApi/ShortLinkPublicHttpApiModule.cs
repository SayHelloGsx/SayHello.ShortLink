using Localization.Resources.AbpUi;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Public;

[DependsOn(typeof(ShortLinkPublicApplicationContractsModule))]
public class ShortLinkPublicHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
        {
            builder.AddApplicationPartIfNotExists(typeof(ShortLinkPublicHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources.Get<ShortLinkResource>().AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
