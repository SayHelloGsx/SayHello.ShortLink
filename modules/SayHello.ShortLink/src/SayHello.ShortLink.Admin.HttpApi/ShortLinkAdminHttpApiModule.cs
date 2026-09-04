using Localization.Resources.AbpUi;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink.Admin;

[DependsOn(typeof(ShortLinkAdminApplicationContractsModule))]
public class ShortLinkAdminHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
        {
            builder.AddApplicationPartIfNotExists(typeof(ShortLinkAdminHttpApiModule).Assembly);
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
