using Localization.Resources.AbpUi;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace SayHello.ShortLink;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(ShortLinkCommonApplicationContractsModule)
)]
public class ShortLinkCommonHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
        {
            builder.AddApplicationPartIfNotExists(typeof(ShortLinkCommonHttpApiModule).Assembly);
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
