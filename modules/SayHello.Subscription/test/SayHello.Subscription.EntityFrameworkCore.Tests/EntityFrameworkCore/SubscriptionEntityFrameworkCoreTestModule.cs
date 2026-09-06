using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SayHello.Subscription.Users;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace SayHello.Subscription.EntityFrameworkCore;

[DependsOn(typeof(SubscriptionEntityFrameworkCoreModule), typeof(SubscriptionDomainTestModule),
    typeof(AbpEntityFrameworkCoreSqliteModule))]
public class SubscriptionEntityFrameworkCoreTestModule : AbpModule
{
    private AbpUnitTestSqliteDatabase _database = null!;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<SubscriptionTestClock>();
        context.Services.Replace(ServiceDescriptor.Singleton<IClock>(provider => provider.GetRequiredService<SubscriptionTestClock>().Clock));
        context.Services.AddSingleton<SubscriptionTestUserDirectory>();
        context.Services.AddSingleton<ISubscriptionUserDirectory>(provider => provider.GetRequiredService<SubscriptionTestUserDirectory>());
        context.Services.AddSingleton<SubscriptionTestDistributedLock>();
        context.Services.Replace(ServiceDescriptor.Singleton<IAbpDistributedLock>(provider => provider.GetRequiredService<SubscriptionTestDistributedLock>()));

        Configure<AbpUnitOfWorkDefaultOptions>(options =>
        {
            options.TransactionBehavior = UnitOfWorkTransactionBehavior.Enabled;
            options.IsolationLevel = IsolationLevel.Serializable;
        });
        _database = new AbpUnitTestSqliteDatabase();
        _database.CreateTables(new SubscriptionDbContext(new DbContextOptionsBuilder<SubscriptionDbContext>()
            .UseSqlite(_database.ConnectionString).Options));
        context.Services.AddSingleton(new SubscriptionTestDatabase(_database.ConnectionString));
        Configure<AbpDbConnectionOptions>(options => options.ConnectionStrings.Default = _database.ConnectionString);
        Configure<AbpDbContextOptions>(options => options.Configure(configuration => configuration.UseSqlite()));
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context) => _database.Dispose();
}
