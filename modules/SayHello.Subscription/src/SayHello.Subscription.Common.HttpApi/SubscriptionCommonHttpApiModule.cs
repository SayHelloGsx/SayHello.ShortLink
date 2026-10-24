using System.Net;
using Localization.Resources.AbpUi;
using Microsoft.Extensions.DependencyInjection;
using SayHello.Subscription.Localization;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace SayHello.Subscription;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(SubscriptionCommonApplicationContractsModule)
)]
public class SubscriptionCommonHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(builder =>
        {
            builder.AddApplicationPartIfNotExists(typeof(SubscriptionCommonHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources.Get<SubscriptionResource>().AddBaseTypes(typeof(AbpUiResource));
        });

        // Stale stamps surface as business errors; keep ABP's 409 Conflict semantics instead of the default 403.
        Configure<AbpExceptionHttpStatusCodeOptions>(options =>
        {
            options.Map(SubscriptionErrorCodes.ConcurrencyConflict, HttpStatusCode.Conflict);
        });
    }
}
