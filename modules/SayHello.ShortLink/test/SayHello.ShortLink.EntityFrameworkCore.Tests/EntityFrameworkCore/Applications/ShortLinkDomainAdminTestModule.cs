using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SayHello.ShortLink.Admin;
using SayHello.ShortLink.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;

namespace SayHello.ShortLink.Admin.Applications;

[DependsOn(
    typeof(ShortLinkEntityFrameworkCoreTestModule),
    typeof(ShortLinkAdminApplicationModule))]
public class ShortLinkDomainAdminTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var settings = Substitute.For<ISettingManagementStore>();
        settings.GetOrNullAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>())
            .Returns((string?)null);
        context.Services.AddSingleton(settings);
        context.Services.AddSingleton(
            Substitute.For<ISettingDefinitionRecordRepository>());
    }
}
