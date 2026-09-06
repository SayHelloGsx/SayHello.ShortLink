using SayHello.Subscription.Localization;
using SayHello.Subscription.Public.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.Subscription.Public;

[DependsOn(typeof(SubscriptionCommonApplicationContractsModule))]
public class SubscriptionPublicApplicationContractsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
            options.FileSets.AddEmbedded<SubscriptionPublicApplicationContractsModule>());
        Configure<AbpLocalizationOptions>(options =>
            options.Resources.Add<SubscriptionPublicResource>("en")
                .AddBaseTypes(typeof(SubscriptionResource))
                .AddVirtualJson("/Localization/SubscriptionPublic"));
    }
}
