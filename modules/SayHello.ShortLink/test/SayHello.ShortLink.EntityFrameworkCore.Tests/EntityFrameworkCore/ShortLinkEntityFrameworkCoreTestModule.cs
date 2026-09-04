using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Common.ShortLinks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;
using Volo.Abp.Uow;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.EntityFrameworkCore;

[DependsOn(
    typeof(ShortLinkApplicationTestModule),
    typeof(ShortLinkEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class ShortLinkEntityFrameworkCoreTestModule : AbpModule
{
    private AbpUnitTestSqliteDatabase _database = null!;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();
        context.Services.AddSingleton<IHostAddressResolver, TestHostAddressResolver>();

        Configure<SettingManagementOptions>(options =>
        {
            options.SaveStaticSettingsToDatabase = false;
            options.IsDynamicSettingStoreEnabled = false;
        });

        Configure<ShortLinkUrlOptions>(options =>
        {
            options.BaseUrl = "https://go.example.test";
        });
        Configure<ShortLinkPrivacyOptions>(options =>
        {
            options.VisitorHashKey = "integration-test-only-key-with-32-bytes";
        });

        _database = new AbpUnitTestSqliteDatabase();
        _database.CreateTables(
            new ShortLinkDbContext(new DbContextOptionsBuilder<ShortLinkDbContext>().UseSqlite(_database.ConnectionString).Options));

        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = _database.ConnectionString;
        });

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(abpDbContextConfigurationContext =>
            {
                abpDbContextConfigurationContext.UseSqlite();
            });
        });
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _database?.Dispose();
    }

}

public class TestHostAddressResolver : IHostAddressResolver
{
    public Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IPAddress> result = [IPAddress.Parse("93.184.216.34")];
        return Task.FromResult(result);
    }
}
