using Microsoft.Extensions.DependencyInjection;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Localization;
using Volo.Abp;
using Volo.Abp.Domain;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;
using Volo.Abp.Validation.Localization;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.Subscription;

[DependsOn(typeof(AbpDddDomainSharedModule), typeof(AbpValidationModule))]
public class SubscriptionDomainSharedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
            options.FileSets.AddEmbedded<SubscriptionDomainSharedModule>());
        Configure<AbpLocalizationOptions>(options =>
            options.Resources.Add<SubscriptionResource>("en")
                .AddBaseTypes(typeof(AbpValidationResource))
                .AddVirtualJson("/Localization/Subscription"));
        Configure<AbpExceptionLocalizationOptions>(options =>
            options.MapCodeNamespace("Subscription", typeof(SubscriptionResource)));
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.GetRequiredService<ISubscriptionDefinitionRegistry>().GetProducts();
    }
}
