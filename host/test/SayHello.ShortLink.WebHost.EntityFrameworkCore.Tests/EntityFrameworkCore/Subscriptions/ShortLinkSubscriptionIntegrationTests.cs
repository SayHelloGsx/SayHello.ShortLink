using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SayHello.ShortLink.Admin.ShortLinks;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

public class ShortLinkSubscriptionIntegrationTests : WebHostTestBase<ShortLinkSubscriptionIntegrationTestModule>
{
    private readonly Guid _owner = Guid.NewGuid();

    [Theory]
    [InlineData(20, false)]
    [InlineData(100, true)]
    public async Task Actual_Free20_And_Pro100_Should_Create_Exactly_Their_Quota(int limit, bool assignPro)
    {
        await RunAsync(async services =>
        {
            services.GetRequiredService<IShortLinkCapabilityProvider>().IsQuotaExternallyManaged.ShouldBeTrue();
            await services.GetRequiredService<ISettingManager>().SetGlobalAsync(ShortLinkSettings.MaxLinksPerUser, "1");
            await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 20, false);
            if (assignPro)
            {
                var pro = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 100, true, setDefault: false);
                await ShortLinkSubscriptionTestData.AssignAsync(services, _owner, pro.Id);
            }
        });

        for (var i = 0; i < limit; i++)
            await CreateAsync();

        var capabilities = await CapabilitiesAsync();
        capabilities.UsedLinks.ShouldBe(limit);
        capabilities.MaxLinks.ShouldBe(limit);
        capabilities.RemainingLinks.ShouldBe(0);
        capabilities.IsQuotaGranted.ShouldBeTrue();
        capabilities.IsUnlimited.ShouldBeFalse();
        capabilities.StatisticsEnabled.ShouldBe(assignPro);
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync()))
            .Code.ShouldBe(ShortLinkErrorCodes.LinkQuotaExceeded);
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(limit);
    }

    [Theory]
    [InlineData("no-product", false, null, false)]
    [InlineData("no-default", false, null, false)]
    [InlineData("missing-quota", false, null, true)]
    [InlineData("zero", true, 0L, false)]
    [InlineData("missing-statistics", true, 1L, false)]
    public async Task Missing_And_Zero_Entitlements_Should_Not_Be_Interpreted_As_Unlimited(
        string source, bool granted, long? limit, bool statistics)
    {
        var tenantId = Guid.NewGuid();
        if (source != "no-product")
        {
            await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(
                services,
                source == "no-default" ? 20 : limit,
                source == "missing-statistics" ? null : statistics,
                setDefault: source != "no-default"), tenantId);
        }

        var capabilities = await CapabilitiesAsync(tenantId);
        capabilities.UsedLinks.ShouldBe(0);
        capabilities.IsQuotaGranted.ShouldBe(granted);
        capabilities.IsUnlimited.ShouldBeFalse();
        capabilities.MaxLinks.ShouldBe(limit);
        capabilities.RemainingLinks.ShouldBe(limit ?? 0);
        capabilities.StatisticsEnabled.ShouldBe(statistics);
        if (!granted || limit == 0)
        {
            (await Should.ThrowAsync<BusinessException>(() => CreateAsync(tenantId)))
                .Code.ShouldBe(granted ? ShortLinkErrorCodes.LinkQuotaExceeded : ShortLinkErrorCodes.LinkQuotaNotGranted);
        }
        else
        {
            (await CreateAsync(tenantId)).TotalVisitCount.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unlimited_And_Long_Quota_Should_Ignore_Legacy_Integer_Setting(bool unlimited)
    {
        await RunAsync(async services =>
        {
            await services.GetRequiredService<ISettingManager>().SetGlobalAsync(ShortLinkSettings.MaxLinksPerUser, "1");
            await ShortLinkSubscriptionTestData.CreatePlanAsync(services, long.MaxValue, true, unlimited);
        });
        await CreateAsync();
        await CreateAsync();
        var capabilities = await CapabilitiesAsync();
        capabilities.IsQuotaGranted.ShouldBeTrue();
        capabilities.IsUnlimited.ShouldBe(unlimited);
        capabilities.UsedLinks.ShouldBe(2);
        capabilities.MaxLinks.ShouldBe(unlimited ? null : long.MaxValue);
        capabilities.RemainingLinks.ShouldBe(unlimited ? null : long.MaxValue - 2);
    }

    [Fact]
    public async Task Unlimited_Should_Not_Bypass_The_Independent_Creation_Rate_Limit()
    {
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, null, true, unlimited: true));
        GetRequiredService<IShortLinkCreationRateLimiter>()
            .EnsureAllowedAsync(_owner, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new BusinessException(ShortLinkErrorCodes.CreationRateExceeded)));
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.CreationRateExceeded);
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(0);
    }

    [Fact]
    public async Task Snapshot_Should_Not_Change_With_Plan_Or_Borrow_Defaults_And_Revoke_Should_Fall_Back()
    {
        Guid planId = Guid.Empty;
        Guid subscriptionId = Guid.Empty;
        await RunAsync(async services =>
        {
            await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 20, true);
            var plan = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 1, null, setDefault: false);
            planId = plan.Id;
            subscriptionId = (await ShortLinkSubscriptionTestData.AssignAsync(services, _owner, plan.Id)).Id;
        });
        await CreateAsync();
        await RunAsync(async services =>
        {
            var repository = services.GetRequiredService<ISubscriptionPlanRepository>();
            var plan = await repository.GetAsync(planId);
            plan.ReplaceEntitlements(ShortLinkSubscriptionTestData.Definition(services),
                ShortLinkSubscriptionTestData.Entitlements(100, true));
            await repository.UpdateAsync(plan, autoSave: true);
        });
        var snapshot = await CapabilitiesAsync();
        snapshot.MaxLinks.ShouldBe(1);
        snapshot.StatisticsEnabled.ShouldBeFalse();
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaExceeded);

        await RunAsync(async services =>
        {
            var subscription = await services.GetRequiredService<IUserSubscriptionRepository>().GetAsync(subscriptionId);
            await services.GetRequiredService<ISubscriptionManager>()
                .RevokeAsync(null, subscription.Id, subscription.ConcurrencyStamp, "integration revoke");
        });
        var fallback = await CapabilitiesAsync();
        fallback.MaxLinks.ShouldBe(20);
        fallback.RemainingLinks.ShouldBe(19);
        fallback.StatisticsEnabled.ShouldBeTrue();
        await CreateAsync();
    }

    [Fact]
    public async Task Explicit_Missing_Quota_Should_Not_Borrow_A_Complete_Default_Plan()
    {
        await RunAsync(async services =>
        {
            await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 20, true);
            var selected = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, null, false, setDefault: false);
            await ShortLinkSubscriptionTestData.AssignAsync(services, _owner, selected.Id);
        });
        var capabilities = await CapabilitiesAsync();
        capabilities.IsQuotaGranted.ShouldBeFalse();
        capabilities.StatisticsEnabled.ShouldBeFalse();
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaNotGranted);
    }

    [Fact]
    public async Task Expired_Snapshot_Should_Use_Live_Default_Then_Deny_When_Default_Is_Cleared()
    {
        await RunAsync(async services =>
        {
            var fallback = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 2, false);
            var expired = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 100, true, setDefault: false);
            var product = await services.GetRequiredService<ISubscriptionProductRepository>().GetAsync(fallback.ProductId);
            var now = services.GetRequiredService<IClock>().Now.ToUniversalTime();
            var subscription = new UserSubscription(Guid.NewGuid(), _owner, product, expired,
                expired.Entitlements.Select(value =>
                    new EntitlementSnapshotData(value.FeatureKey, value.FeatureKey, value.ToValue())).ToArray(),
                now.AddDays(-2), now.AddDays(-1), Guid.NewGuid());
            await services.GetRequiredService<IUserSubscriptionRepository>().InsertAsync(subscription, autoSave: true);
        });
        var fallback = await CapabilitiesAsync();
        fallback.MaxLinks.ShouldBe(2);
        fallback.StatisticsEnabled.ShouldBeFalse();
        await CreateAsync();
        await RunAsync(services => ShortLinkSubscriptionTestData.SetDefaultAsync(services, null));
        var cleared = await CapabilitiesAsync();
        cleared.UsedLinks.ShouldBe(1);
        cleared.IsQuotaGranted.ShouldBeFalse();
        cleared.RemainingLinks.ShouldBe(0);
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaNotGranted);
    }

    [Fact]
    public async Task Downgrade_Should_Keep_Existing_Links_And_Recover_After_Default_Changes()
    {
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 3, true));
        var first = await CreateAsync();
        await CreateAsync();
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 1, false));
        var overQuota = await CapabilitiesAsync();
        overQuota.UsedLinks.ShouldBe(2);
        overQuota.RemainingLinks.ShouldBe(0);
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaExceeded);
        (await RunAsync(services => services.GetRequiredService<IShortLinkAppService>().GetAsync(first.Id)))
            .Status.ShouldBe(ShortLinkStatus.Active);
        (await RunAsync(services => services.GetRequiredService<IShortLinkAppService>().GetQrCodeAsync(first.Id)))
            .Content.ShouldContain("<svg");
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 3, true));
        await CreateAsync();
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(3);
    }

    [Fact]
    public async Task Host_Tenant_And_Owner_Usage_And_Defaults_Should_Be_Isolated()
    {
        var firstTenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 1, true));
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 2, false), firstTenant);
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 0, true), secondTenant);
        await CreateAsync();
        var tenantLink = await CreateAsync(firstTenant);
        await CreateAsync(firstTenant);
        await CreateAsync(firstTenant, otherOwner);
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(1);
        (await CapabilitiesAsync(firstTenant)).UsedLinks.ShouldBe(2);
        (await CapabilitiesAsync(firstTenant, otherOwner)).UsedLinks.ShouldBe(1);
        (await CapabilitiesAsync(secondTenant)).UsedLinks.ShouldBe(0);
        (await CapabilitiesAsync(secondTenant)).MaxLinks.ShouldBe(0);
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync(firstTenant))).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaExceeded);
        await Should.ThrowAsync<EntityNotFoundException>(() => RunAsync(
            services => services.GetRequiredService<IShortLinkAppService>().GetAsync(tenantLink.Id), secondTenant));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disabled_Expired_Links_Should_Count_And_Committed_User_Or_Admin_Deletion_Should_Release(bool admin)
    {
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 2, false));
        var expired = await RunAsync(services => services.GetRequiredService<IShortLinkAppService>()
            .CreateAsync(ShortLinkSubscriptionTestData.NewLink(DateTime.UtcNow.AddDays(-1))));
        var disabled = await CreateAsync();
        await RunAsync(services => services.GetRequiredService<IShortLinkAppService>().SetStatusAsync(disabled.Id,
            new SetShortLinkStatusDto { Status = ShortLinkStatus.Disabled, ConcurrencyStamp = disabled.ConcurrencyStamp }));
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(2);
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaExceeded);

        await DeleteAsync(expired.Id, admin, commit: false);
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(2);
        await DeleteAsync(expired.Id, admin, commit: true);
        await RunAsync(async services =>
        {
            using var deletedFilter = services.GetRequiredService<IDataFilter<ISoftDelete>>().Disable();
            (await services.GetRequiredService<IShortLinkRepository>().GetAsync(expired.Id)).IsDeleted.ShouldBeTrue();
            (await services.GetRequiredService<IShortLinkAppService>().GetCapabilitiesAsync()).UsedLinks.ShouldBe(1);
        });
        await CreateAsync();
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(2);
    }

    [Fact]
    public async Task Invalid_Url_And_Rolled_Back_Create_Should_Not_Consume_Subscription_Capacity()
    {
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(services, 1, true));
        (await Should.ThrowAsync<BusinessException>(() => RunAsync(services =>
            services.GetRequiredService<IShortLinkAppService>().CreateAsync(new CreateShortLinkDto
            {
                TargetUrl = "https://127.0.0.1/private",
                CustomCode = "invalid" + Guid.NewGuid().ToString("N")
            })))).Code.ShouldBe(ShortLinkErrorCodes.UnsafeTargetUrl);
        await RunAsync(services => services.GetRequiredService<IShortLinkAppService>()
            .CreateAsync(ShortLinkSubscriptionTestData.NewLink()), commit: false);
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(0);
        await CreateAsync();
        (await CapabilitiesAsync()).UsedLinks.ShouldBe(1);
    }

    private Task DeleteAsync(Guid id, bool admin, bool commit) => RunAsync(
        services => admin
            ? services.GetRequiredService<IShortLinkAdministrationAppService>().DeleteAsync(id)
            : services.GetRequiredService<IShortLinkAppService>().DeleteAsync(id),
        userId: admin ? Guid.NewGuid() : _owner, commit: commit);

    private Task<ShortLinkDto> CreateAsync(Guid? tenantId = null, Guid? userId = null) =>
        RunAsync(services => services.GetRequiredService<IShortLinkAppService>()
            .CreateAsync(ShortLinkSubscriptionTestData.NewLink()), tenantId, userId);

    private Task<ShortLinkCapabilitiesDto> CapabilitiesAsync(Guid? tenantId = null, Guid? userId = null) =>
        RunAsync(services => services.GetRequiredService<IShortLinkAppService>().GetCapabilitiesAsync(), tenantId, userId);

    private Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action,
        Guid? tenantId = null, Guid? userId = null, bool commit = true) =>
        ShortLinkSubscriptionTestData.RunAsync(ServiceProvider, tenantId, userId ?? _owner, action, commit);

    private Task RunAsync(Func<IServiceProvider, Task> action,
        Guid? tenantId = null, Guid? userId = null, bool commit = true) =>
        ShortLinkSubscriptionTestData.RunAsync(ServiceProvider, tenantId, userId ?? _owner, action, commit);
}
