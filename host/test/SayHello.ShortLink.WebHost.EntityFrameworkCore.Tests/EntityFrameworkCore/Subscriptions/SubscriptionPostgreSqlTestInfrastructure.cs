using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SayHello.Subscription;
using SayHello.Subscription.Definitions;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DistributedLocking;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SubscriptionPostgreSqlDatabase.EnvironmentVariable)))
            Skip = $"Set {SubscriptionPostgreSqlDatabase.EnvironmentVariable} to opt into isolated PostgreSQL 17 tests.";
    }
}

public sealed class PostgreSqlTheoryAttribute : TheoryAttribute
{
    public PostgreSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SubscriptionPostgreSqlDatabase.EnvironmentVariable)))
            Skip = $"Set {SubscriptionPostgreSqlDatabase.EnvironmentVariable} to opt into isolated PostgreSQL 17 tests.";
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SubscriptionPostgreSqlCollection
{
    public const string Name = "Subscription PostgreSQL";
}

internal sealed class SubscriptionPostgreSqlDatabase : IAsyncDisposable
{
    public const string EnvironmentVariable = "SUBSCRIPTION_TEST_POSTGRES_CONNECTION_STRING";
    private readonly string _adminConnectionString;
    private readonly string _databaseName = "subscription_test_" + Guid.NewGuid().ToString("N");
    private bool _created;

    public string ConnectionString { get; }

    public SubscriptionPostgreSqlDatabase()
    {
        var admin = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(EnvironmentVariable)
            ?? throw new InvalidOperationException($"Set {EnvironmentVariable} before running PostgreSQL tests."))
        {
            Pooling = false,
            Enlist = false
        };
        _adminConnectionString = admin.ConnectionString;
        admin.Database = _databaseName;
        admin.SearchPath = "public";
        ConnectionString = admin.ConnectionString;
    }

    public async Task CreateAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        Assert.Equal(17, admin.PostgreSqlVersion.Major);
        await using var command = admin.CreateCommand();
        // The caller's database is administrative access only: migrations always use this generated name.
        command.CommandText = $"""CREATE DATABASE "{_databaseName}" TEMPLATE template0;""";
        await command.ExecuteNonQueryAsync();
        _created = true;
    }

    public WebHostDbContext CreateContext()
    {
        if (!_created) throw new InvalidOperationException("The isolated test database has not been created.");
        WebHostEfCoreEntityExtensionMappings.Configure();
        return new WebHostDbContext(new DbContextOptionsBuilder<WebHostDbContext>()
            .UseNpgsql(ConnectionString).Options);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created) return;
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var command = admin.CreateCommand();
        // Never drop a supplied database, clear unrelated pools, or terminate unrelated connections.
        command.CommandText = $"""DROP DATABASE "{_databaseName}" WITH (FORCE);""";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "SELECT count(*) FROM pg_database WHERE datname = @name;";
        command.Parameters.AddWithValue("name", _databaseName);
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        _created = false;
    }
}

[DependsOn(typeof(WebHostEntityFrameworkCoreModule), typeof(AbpAutofacModule))]
public class SubscriptionPostgreSqlTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });
        Configure<SettingManagementOptions>(options =>
        {
            options.SaveStaticSettingsToDatabase = false;
            options.IsDynamicSettingStoreEnabled = false;
        });
        Configure<SubscriptionDefinitionOptions>(options =>
            options.DefinitionProviders.Add<SubscriptionPostgreSqlDefinitions>());
        context.Services.AddSingleton<IAbpDistributedLock, InProcessAbpDistributedLock>();
        context.Services.AddSingleton<SubscriptionPostgreSqlClock>();
        context.Services.Replace(ServiceDescriptor.Singleton<IClock>(provider =>
            provider.GetRequiredService<SubscriptionPostgreSqlClock>()));
    }
}

public class SubscriptionPostgreSqlDefinitions : SubscriptionDefinitionProvider
{
    public override void Define(ISubscriptionDefinitionContext context)
    {
        foreach (var code in new[] { "postgres-beta", "postgres-gamma" })
            context.AddProduct(new ProductDefinition(code, new FixedLocalizableString(code),
                [new FeatureDefinition("enabled", new FixedLocalizableString("Enabled"), SubscriptionEntitlementType.Boolean)]));
    }
}

public sealed class SubscriptionPostgreSqlClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
    public DateTimeKind Kind => DateTimeKind.Unspecified;
    public bool SupportsMultipleTimezone => false;
    public DateTime Normalize(DateTime dateTime) => dateTime;
    public DateTime ConvertToUserTime(DateTime dateTime) => dateTime;
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTime) => dateTime;
    public DateTime ConvertToUtc(DateTime dateTime) => dateTime.ToUniversalTime();
}
