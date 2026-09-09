using SayHello.ShortLink.Subscription.Localization;
using SayHello.Subscription;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.ShortLink.Subscription;

[DependsOn(typeof(SubscriptionDomainSharedModule))]
public class ShortLinkSubscriptionDomainSharedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<SubscriptionDefinitionOptions>(options =>
            options.DefinitionProviders.Add<ShortLinkSubscriptionDefinitionProvider>());
        Configure<AbpVirtualFileSystemOptions>(options =>
            options.FileSets.AddEmbedded<ShortLinkSubscriptionDomainSharedModule>());
        Configure<AbpLocalizationOptions>(options =>
            options.Resources.Add<ShortLinkSubscriptionResource>("en")
                .AddBaseTypes(typeof(SubscriptionResource))
                .AddVirtualJson("/Localization/ShortLinkSubscription"));
    }
}
