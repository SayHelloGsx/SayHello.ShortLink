using SayHello.Subscription.Admin.Localization;
using SayHello.Subscription.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace SayHello.Subscription.Admin;

[DependsOn(typeof(SubscriptionCommonApplicationContractsModule))]
public class SubscriptionAdminApplicationContractsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
            options.FileSets.AddEmbedded<SubscriptionAdminApplicationContractsModule>());
        Configure<AbpLocalizationOptions>(options =>
            options.Resources.Add<SubscriptionAdminResource>("en")
                .AddBaseTypes(typeof(SubscriptionResource))
                .AddVirtualJson("/Localization/SubscriptionAdmin"));
    }
}
