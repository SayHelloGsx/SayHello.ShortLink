using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.ShortLinks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

[DependsOn(typeof(SubscriptionPostgreSqlTestModule))]
public class ShortLinkSubscriptionPostgreSqlTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        ShortLinkSubscriptionTestData.ConfigureExternalAdapters(context.Services);
}

[Collection(SubscriptionPostgreSqlCollection.Name)]
public class ShortLinkSubscriptionPostgreSqlTests : IAsyncLifetime
{
    private readonly SubscriptionPostgreSqlDatabase _database = new();
    private IAbpApplicationWithInternalServiceProvider? _application;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        await _database.CreateAsync();
        await using (var context = _database.CreateContext())
            await context.Database.MigrateAsync();
        _application = await AbpApplicationFactory.CreateAsync<ShortLinkSubscriptionPostgreSqlTestModule>(options =>
        {
            options.UseAutofac();
            options.Services.Configure<AbpDbConnectionOptions>(connections =>
                connections.ConnectionStrings.Default = _database.ConnectionString);
        });
        await _application.InitializeAsync();
        _initialized = true;
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_Subscription_Quota_Should_Allow_Only_One_Concurrent_Commit_In_Separate_Scopes(bool hasTenant)
    {
        var tenantId = hasTenant ? Guid.NewGuid() : (Guid?)null;
        var owner = Guid.NewGuid();
        await RunAsync(tenantId, owner, services =>
            ShortLinkSubscriptionTestData.CreatePlanAsync(services, 1, true));

        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            try
            {
                await CreateAsync(tenantId, owner);
                return "created";
            }
            catch (BusinessException exception)
            {
                return exception.Code;
            }
        }));
        attempts.Count(result => result == "created").ShouldBe(1);
        attempts.Count(result => result == ShortLinkErrorCodes.LinkQuotaExceeded).ShouldBe(5);
        (await RunAsync(tenantId, owner, services =>
            services.GetRequiredService<IShortLinkRepository>().GetCountByOwnerAsync(owner, tenantId))).ShouldBe(1);
    }

    [PostgreSqlFact]
    public async Task Rolled_Back_Create_Should_Release_The_Lock_And_Not_Occupy_Actual_Subscription_Quota()
    {
        var owner = Guid.NewGuid();
        await RunAsync(null, owner, services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 1, true));
        await CreateAsync(null, owner, commit: false);
        (await RunAsync(null, owner, services =>
            services.GetRequiredService<IShortLinkRepository>().GetCountByOwnerAsync(owner, null))).ShouldBe(0);
        await CreateAsync(null, owner);
        (await RunAsync(null, owner, services =>
            services.GetRequiredService<IShortLinkRepository>().GetCountByOwnerAsync(owner, null))).ShouldBe(1);
    }

    private Task CreateAsync(Guid? tenantId, Guid owner, bool commit = true) =>
        ShortLinkSubscriptionTestData.RunAsync(_application!.ServiceProvider, tenantId, owner, async services =>
        {
            var input = ShortLinkSubscriptionTestData.NewLink();
            await services.GetRequiredService<ShortLinkManager>().CreateAndSaveAsync(
                Guid.NewGuid(), tenantId, owner, input.TargetUrl, input.CustomCode, input.Title, null);
        }, commit);

    private Task<T> RunAsync<T>(Guid? tenantId, Guid owner, Func<IServiceProvider, Task<T>> action) =>
        ShortLinkSubscriptionTestData.RunAsync(_application!.ServiceProvider, tenantId, owner, action);

    public async Task DisposeAsync()
    {
        try
        {
            if (_initialized) await _application!.ShutdownAsync();
        }
        finally
        {
            try
            {
                _application?.Dispose();
            }
            finally
            {
                await _database.DisposeAsync();
            }
        }
    }
}
