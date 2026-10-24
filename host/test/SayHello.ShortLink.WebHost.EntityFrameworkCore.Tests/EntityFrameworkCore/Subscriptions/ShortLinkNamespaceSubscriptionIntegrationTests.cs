using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Admin.ShortLinkDomains;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinkNamespaces;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using SayHello.ShortLink.WebHost.Data;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Timing;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

public class ShortLinkNamespaceSubscriptionIntegrationTests :
    WebHostTestBase<ShortLinkSubscriptionIntegrationTestModule>
{
    private readonly Guid _owner = Guid.NewGuid();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Default_User_Role_Should_Receive_Namespace_Permissions_Idempotently(bool tenant)
    {
        Guid? tenantId = tenant ? Guid.NewGuid() : null;
        for (var i = 0; i < 2; i++)
            await ShortLinkSubscriptionTestData.RunAsync(ServiceProvider, tenantId, _owner, services =>
                services.GetRequiredService<ShortLinkRoleDataSeedContributor>().SeedAsync(new DataSeedContext(tenantId)));

        await ShortLinkSubscriptionTestData.RunAsync(ServiceProvider, tenantId, _owner, async services =>
        {
            var names = new[]
            {
                ShortLinkPublicPermissions.Namespaces.Default,
                ShortLinkPublicPermissions.Namespaces.Create,
                ShortLinkPublicPermissions.Namespaces.Delete
            };
            var grants = (await services.GetRequiredService<IPermissionGrantRepository>().GetListAsync(
                    RolePermissionValueProvider.ProviderName, ShortLinkRoleDataSeedContributor.UserRoleName))
                .Where(grant => names.Contains(grant.Name))
                .Select(grant => grant.Name).ToList();
            grants.Count.ShouldBe(3);
            foreach (var name in names)
                grants.ShouldContain(name);
        });
    }

    [Theory]
    [InlineData("missing", false, null)]
    [InlineData("zero", true, 0L)]
    [InlineData("finite", true, 1L)]
    [InlineData("unlimited", true, null)]
    public async Task Real_Namespace_And_Root_Quotas_Should_Distinguish_All_Grant_States(
        string mode, bool granted, long? limit)
    {
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(
            services, 10, false,
            namespaceLimit: limit, namespacesUnlimited: mode == "unlimited",
            rootLinkLimit: limit, rootLinksUnlimited: mode == "unlimited"));
        var ordinary = await CreateLinkAsync();
        ordinary.Namespace.ShouldBe("s");
        ordinary.ShortUrl.ShouldBe($"{ordinary.Origin}/s/{ordinary.Code}");
        var capabilities = await CapabilitiesAsync();
        capabilities.NamespacesEnabled.ShouldBe(mode is "finite" or "unlimited");
        capabilities.IsNamespaceQuotaGranted.ShouldBe(granted);
        capabilities.IsRootLinkQuotaGranted.ShouldBe(granted);
        capabilities.IsNamespaceUnlimited.ShouldBe(mode == "unlimited");
        capabilities.IsRootLinkUnlimited.ShouldBe(mode == "unlimited");
        capabilities.MaxNamespaces.ShouldBe(limit);
        capabilities.MaxRootLinks.ShouldBe(limit);
        capabilities.RemainingNamespaces.ShouldBe(mode == "unlimited" ? null : limit ?? 0);
        capabilities.RemainingRootLinks.ShouldBe(mode == "unlimited" ? null : limit ?? 0);

        if (!granted || limit == 0)
        {
            capabilities.CanCreateNamespace.ShouldBeFalse();
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync()))
                .Code.ShouldBe(ShortLinkErrorCodes.NamespacesNotGranted);
            (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(root: true))).Code.ShouldBe(
                granted ? ShortLinkErrorCodes.RootLinkQuotaExceeded : ShortLinkErrorCodes.RootLinkQuotaNotGranted);
            return;
        }

        var count = mode == "unlimited" ? 2 : 1;
        for (var i = 0; i < count; i++)
        {
            await CreateNamespaceAsync();
            await CreateLinkAsync(root: true);
        }
        var used = await CapabilitiesAsync();
        used.UsedNamespaces.ShouldBe(count);
        used.UsedRootLinks.ShouldBe(count);
        if (mode == "finite")
        {
            used.CanCreateNamespace.ShouldBeFalse();
            used.CanCreateRootLink.ShouldBeFalse();
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync()))
                .Code.ShouldBe(ShortLinkErrorCodes.NamespaceQuotaExceeded);
            (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(root: true)))
                .Code.ShouldBe(ShortLinkErrorCodes.RootLinkQuotaExceeded);
        }
    }

    [Fact]
    public async Task Assigned_Snapshot_Should_Retain_New_Benefits_After_Plan_Edit_And_Revoke_Should_Use_Default()
    {
        Guid planId = Guid.Empty;
        Guid subscriptionId = Guid.Empty;
        await RunAsync(async services =>
        {
            await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 10, false, namespaceLimit: 0, rootLinkLimit: 0);
            var plan = await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 10, false, setDefault: false,
                namespaceLimit: 1, rootLinkLimit: 1);
            planId = plan.Id;
            subscriptionId = (await ShortLinkSubscriptionTestData.AssignAsync(services, _owner, plan.Id)).Id;
        });
        var item = await CreateNamespaceAsync();
        var root = await CreateLinkAsync(root: true);
        var namespaced = await CreateLinkAsync(namespaceId: item.Id);
        await RunAsync(async services =>
        {
            var repository = services.GetRequiredService<ISubscriptionPlanRepository>();
            var plan = await repository.GetAsync(planId);
            plan.ReplaceEntitlements(ShortLinkSubscriptionTestData.Definition(services),
                ShortLinkSubscriptionTestData.Entitlements(
                    10, false, namespaceLimit: 0, rootLinkLimit: 0));
            await repository.UpdateAsync(plan, autoSave: true);
        });
        var snapshot = await CapabilitiesAsync();
        snapshot.NamespacesEnabled.ShouldBeTrue();
        snapshot.MaxNamespaces.ShouldBe(1);
        snapshot.MaxRootLinks.ShouldBe(1);
        (await CreateLinkAsync(namespaceId: item.Id)).ShouldNotBeNull();

        await RevokeAsync(subscriptionId);
        var fallback = await CapabilitiesAsync();
        fallback.NamespacesEnabled.ShouldBeFalse();
        fallback.MaxNamespaces.ShouldBe(0);
        fallback.MaxRootLinks.ShouldBe(0);
        (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(namespaceId: item.Id)))
            .Code.ShouldBe(ShortLinkErrorCodes.NamespacesNotGranted);
        (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(root: true)))
            .Code.ShouldBe(ShortLinkErrorCodes.RootLinkQuotaExceeded);
        (await CreateLinkAsync()).Namespace.ShouldBe("s");
        await RunAsync(async services =>
        {
            var app = services.GetRequiredService<IShortLinkAppService>();
            (await app.GetAsync(root.Id)).ShortUrl.ShouldBe(root.ShortUrl);
            (await app.GetAsync(namespaced.Id)).ShortUrl.ShouldBe(namespaced.ShortUrl);
            (await services.GetRequiredService<IShortLinkNamespaceAppService>().GetListAsync())
                .Items.Single().Id.ShouldBe(item.Id);
            var resolved = await services.GetRequiredService<IShortLinkRedirectAppService>()
                .ResolveAsync(item.Origin, namespaced.Namespace, namespaced.Code);
            resolved.Status.ShouldBe(ShortLinkResolutionStatus.Found);
            resolved.TargetUrl.ShouldBe(namespaced.TargetUrl);
        });
    }

    [Fact]
    public async Task Old_Snapshot_With_Missing_New_Benefits_Should_Not_Borrow_Complete_Default_Grants()
    {
        Guid subscriptionId = Guid.Empty;
        await RunAsync(async services =>
        {
            await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 10, false, namespacesUnlimited: true, rootLinksUnlimited: true);
            var oldPlan = await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 10, false, setDefault: false);
            subscriptionId = (await ShortLinkSubscriptionTestData.AssignAsync(services, _owner, oldPlan.Id)).Id;
        });
        var explicitSnapshot = await CapabilitiesAsync();
        explicitSnapshot.NamespacesEnabled.ShouldBeFalse();
        explicitSnapshot.IsNamespaceQuotaGranted.ShouldBeFalse();
        explicitSnapshot.IsRootLinkQuotaGranted.ShouldBeFalse();
        (await CreateLinkAsync()).Namespace.ShouldBe("s");
        (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync()))
            .Code.ShouldBe(ShortLinkErrorCodes.NamespacesNotGranted);
        (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(root: true)))
            .Code.ShouldBe(ShortLinkErrorCodes.RootLinkQuotaNotGranted);

        await RevokeAsync(subscriptionId);
        var fallback = await CapabilitiesAsync();
        fallback.NamespacesEnabled.ShouldBeTrue();
        fallback.IsNamespaceUnlimited.ShouldBeTrue();
        fallback.IsRootLinkUnlimited.ShouldBeTrue();
        await CreateNamespaceAsync();
        await CreateLinkAsync(root: true);
    }

    [Fact]
    public async Task Expired_Namespace_Snapshot_Should_Use_Current_Default_Rather_Than_Old_Unlimited_Rights()
    {
        await RunAsync(async services =>
        {
            var fallback = await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 10, false, namespaceLimit: 0, rootLinkLimit: 0);
            var expired = await ShortLinkSubscriptionTestData.CreatePlanAsync(
                services, 10, false, setDefault: false,
                namespacesUnlimited: true, rootLinksUnlimited: true);
            var product = await services.GetRequiredService<ISubscriptionProductRepository>().GetAsync(fallback.ProductId);
            var now = services.GetRequiredService<IClock>().Now.ToUniversalTime();
            var subscription = new UserSubscription(
                Guid.NewGuid(), _owner, product, expired,
                expired.Entitlements.Select(value =>
                    new EntitlementSnapshotData(value.FeatureKey, value.FeatureKey, value.ToValue())).ToArray(),
                now.AddDays(-2), now.AddDays(-1), Guid.NewGuid());
            await services.GetRequiredService<IUserSubscriptionRepository>().InsertAsync(subscription, autoSave: true);
        });
        var capabilities = await CapabilitiesAsync();
        capabilities.NamespacesEnabled.ShouldBeFalse();
        capabilities.IsNamespaceUnlimited.ShouldBeFalse();
        capabilities.IsRootLinkUnlimited.ShouldBeFalse();
        capabilities.MaxNamespaces.ShouldBe(0);
        capabilities.MaxRootLinks.ShouldBe(0);
        await CreateLinkAsync();
        (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync()))
            .Code.ShouldBe(ShortLinkErrorCodes.NamespacesNotGranted);
        (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(root: true)))
            .Code.ShouldBe(ShortLinkErrorCodes.RootLinkQuotaExceeded);
    }

    [Fact]
    public async Task Empty_Namespace_Should_Protect_Domain_And_Its_Admin_Delete_Capability()
    {
        const string origin = "https://namespace-domain.example.test";
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(
            services, 10, false, domains: [origin], namespaceLimit: 1));
        var domain = await RunAsync(services =>
            services.GetRequiredService<IShortLinkDomainAppService>().CreateAsync(
                new CreateShortLinkDomainDto { Origin = origin }));
        domain.CanDelete.ShouldBeTrue();
        domain.HasNamespaceReferences.ShouldBeFalse();
        var item = await CreateNamespaceAsync(origin);
        await RunAsync(async services =>
        {
            var populated = (await services.GetRequiredService<IShortLinkDomainAppService>().GetListAsync())
                .Items.Single(value => value.Id == domain.Id);
            populated.ShortLinkReferenceCount.ShouldBe(0);
            populated.HasNamespaceReferences.ShouldBeTrue();
            populated.CanDelete.ShouldBeFalse();
        });
        (await Should.ThrowAsync<BusinessException>(() => RunAsync(services =>
                services.GetRequiredService<IShortLinkDomainAppService>().DeleteAsync(
                    domain.Id, new DeleteShortLinkDomainDto { ConcurrencyStamp = domain.ConcurrencyStamp }))))
            .Code.ShouldBe(ShortLinkErrorCodes.DomainInUse);
        await RunAsync(services => services.GetRequiredService<IShortLinkNamespaceAppService>().DeleteAsync(
            item.Id, new DeleteShortLinkNamespaceDto { ConcurrencyStamp = item.ConcurrencyStamp }));
        await RunAsync(async services =>
        {
            var app = services.GetRequiredService<IShortLinkDomainAppService>();
            var empty = (await app.GetListAsync()).Items.Single(value => value.Id == domain.Id);
            empty.HasNamespaceReferences.ShouldBeFalse();
            empty.CanDelete.ShouldBeTrue();
            await app.DeleteAsync(domain.Id, new DeleteShortLinkDomainDto { ConcurrencyStamp = empty.ConcurrencyStamp });
        });
    }

    [Fact]
    public async Task Root_Total_And_Custom_Quotas_Should_Stack_And_Rollback_Should_Not_Release_Capacity()
    {
        await RunAsync(services => ShortLinkSubscriptionTestData.CreatePlanAsync(
            services, 2, false, customCodeLimit: 1, customCodesUnlimited: false, rootLinkLimit: 1));
        var root = await CreateLinkAsync(root: true);
        (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync()))
            .Code.ShouldBe(ShortLinkErrorCodes.CustomCodeQuotaExceeded);
        (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(root: true, custom: false)))
            .Code.ShouldBe(ShortLinkErrorCodes.RootLinkQuotaExceeded);
        await CreateLinkAsync(custom: false);
        (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(root: true, custom: false)))
            .Code.ShouldBe(ShortLinkErrorCodes.LinkQuotaExceeded);

        var deleteRoot = new DeleteShortLinkDto { ConcurrencyStamp = root.ConcurrencyStamp };
        await RunAsync(services => services.GetRequiredService<IShortLinkAppService>().DeleteAsync(root.Id, deleteRoot),
            commit: false);
        var rolledBack = await CapabilitiesAsync();
        rolledBack.UsedLinks.ShouldBe(2);
        rolledBack.UsedRootLinks.ShouldBe(1);
        rolledBack.UsedCustomCodes.ShouldBe(1);
        await RunAsync(services => services.GetRequiredService<IShortLinkAppService>().DeleteAsync(root.Id, deleteRoot));
        var released = await CapabilitiesAsync();
        released.UsedLinks.ShouldBe(1);
        released.UsedRootLinks.ShouldBe(0);
        released.UsedCustomCodes.ShouldBe(0);
        (await CreateLinkAsync(root: true)).ShouldNotBeNull();
    }

    private Task RevokeAsync(Guid id) => RunAsync(async services =>
    {
        var subscription = await services.GetRequiredService<IUserSubscriptionRepository>().GetAsync(id);
        await services.GetRequiredService<ISubscriptionManager>()
            .RevokeAsync(id, subscription.ConcurrencyStamp, "namespace integration revoke");
    });

    private Task<ShortLinkNamespaceDto> CreateNamespaceAsync(string? origin = null) => RunAsync(services =>
        services.GetRequiredService<IShortLinkNamespaceAppService>().CreateAsync(new CreateShortLinkNamespaceDto
        {
            Origin = origin ?? ShortLinkSubscriptionTestData.DefaultOrigin,
            Name = "host" + Guid.NewGuid().ToString("N")
        }));

    private Task<ShortLinkDto> CreateLinkAsync(bool root = false, bool custom = true, Guid? namespaceId = null)
    {
        var input = ShortLinkSubscriptionTestData.NewLink(customCode: custom);
        input.UseRootPath = root;
        input.NamespaceId = namespaceId;
        return RunAsync(services => services.GetRequiredService<IShortLinkAppService>().CreateAsync(input));
    }

    private Task<ShortLinkCapabilitiesDto> CapabilitiesAsync() => RunAsync(services =>
        services.GetRequiredService<IShortLinkAppService>().GetCapabilitiesAsync());

    private Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action, bool commit = true) =>
        ShortLinkSubscriptionTestData.RunAsync(ServiceProvider, null, _owner, action, commit);

    private Task RunAsync(Func<IServiceProvider, Task> action, bool commit = true) =>
        ShortLinkSubscriptionTestData.RunAsync(ServiceProvider, null, _owner, action, commit);
}
