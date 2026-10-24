using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using QRCoder;
using SayHello.ShortLink.Admin.Settings;
using SayHello.ShortLink.Admin.ShortLinkDomains;
using SayHello.ShortLink.Admin.ShortLinks;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinkNamespaces;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using Volo.Abp.Validation;
using Xunit;
using DomainAdministration = SayHello.ShortLink.Admin.ShortLinkDomains.IShortLinkDomainAppService;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkNamespaceUseCaseTests :
    ShortLinkTestBase<ShortLinkCapabilitiesTestModule>,
    IAsyncLifetime
{
    private const string Origin = "https://go.example.test";
    private QuotaCapabilityProvider _capabilities = null!;
    private QuotaTrackingLock _locks = null!;
    private IShortLinkUserEligibilityChecker _eligibility = null!;
    private string? _deniedPermission;
    private readonly IShortLinkNamespaceAppService _namespaces;
    private readonly IShortLinkAppService _links;
    private readonly IShortLinkRedirectAppService _redirect;
    private readonly IShortLinkAdministrationAppService _admin;
    private readonly IShortLinkSettingsAppService _settings;
    private readonly IShortLinkNamespaceRepository _namespaceRepository;
    private readonly IShortLinkNamespaceReservationRepository _reservations;
    private readonly IShortLinkRepository _repository;
    private readonly IDistributedCache<ShortLinkResolutionCacheItem, string> _cache;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _units;
    private readonly UnitOfWorkSettingStore _settingStore;

    public ShortLinkNamespaceUseCaseTests()
    {
        _namespaces = GetRequiredService<IShortLinkNamespaceAppService>();
        _links = GetRequiredService<IShortLinkAppService>();
        _redirect = GetRequiredService<IShortLinkRedirectAppService>();
        _admin = GetRequiredService<IShortLinkAdministrationAppService>();
        _settings = GetRequiredService<IShortLinkSettingsAppService>();
        _namespaceRepository = GetRequiredService<IShortLinkNamespaceRepository>();
        _reservations = GetRequiredService<IShortLinkNamespaceReservationRepository>();
        _repository = GetRequiredService<IShortLinkRepository>();
        _cache = GetRequiredService<IDistributedCache<ShortLinkResolutionCacheItem, string>>();
        _currentUser = GetRequiredService<ICurrentUser>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _units = GetRequiredService<IUnitOfWorkManager>();
        _settingStore = GetRequiredService<UnitOfWorkSettingStore>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        base.AfterAddApplication(services);
        _capabilities = new QuotaCapabilityProvider();
        _locks = new QuotaTrackingLock();
        _eligibility = Substitute.For<IShortLinkUserEligibilityChecker>();
        _eligibility.EnsureEligibleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var rateLimiter = Substitute.For<IShortLinkCreationRateLimiter>();
        rateLimiter.EnsureAllowedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var permissions = Substitute.For<IPermissionChecker>();
        permissions.IsGrantedAsync(Arg.Any<string>()).Returns(call =>
            GetRequiredService<ICurrentUser>().IsAuthenticated &&
            call.Arg<string>() != _deniedPermission);
        permissions.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>()).Returns(call =>
            call.ArgAt<ClaimsPrincipal>(0).FindFirst(AbpClaimTypes.UserId) is not null &&
            call.ArgAt<string>(1) != _deniedPermission);

        services.AddSingleton<IShortLinkCapabilityProvider>(_capabilities);
        services.AddSingleton<IAbpDistributedLock>(_locks);
        services.AddSingleton(_eligibility);
        services.AddSingleton(rateLimiter);
        services.AddSingleton(permissions);
        services.Replace(ServiceDescriptor.Transient<IAuthorizationService, AbpAuthorizationService>());
        services.Replace(ServiceDescriptor.Transient<IAbpAuthorizationService, AbpAuthorizationService>());
        services.Replace(ServiceDescriptor.Transient<IMethodInvocationAuthorizationService, MethodInvocationAuthorizationService>());
        services.AddSingleton<UnitOfWorkSettingStore>();
        services.AddSingleton<ISettingManagementStore>(provider =>
            provider.GetRequiredService<UnitOfWorkSettingStore>());
    }

    public async Task InitializeAsync()
    {
        await CreateDomainAsync(Origin);
        _locks.ResetAcquisitions();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Namespace_Api_Should_Return_Exact_Identity_And_Filter_Normalized_Origins()
    {
        var domain = await CreateDomainAsync("https://alt.example.test");
        var first = await CreateNamespaceAsync("My_Campaign", "HTTPS://GO.EXAMPLE.TEST:443/");
        var second = await CreateNamespaceAsync("My_Campaign", domain.Origin);

        first.Id.ShouldNotBe(Guid.Empty);
        first.Origin.ShouldBe(Origin);
        first.Name.ShouldBe("My_Campaign");
        first.CreationTime.ShouldNotBe(default);
        first.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace();
        first.DomainId.ShouldNotBe(second.DomainId);
        (await _namespaces.GetListAsync()).Items.Count.ShouldBe(2);
        (await _namespaces.GetListAsync("HTTPS://GO.EXAMPLE.TEST:443/")).Items
            .ShouldHaveSingleItem().Id.ShouldBe(first.Id);
        (await _namespaces.GetListAsync(domain.Origin)).Items
            .ShouldHaveSingleItem().Id.ShouldBe(second.Id);
        typeof(ShortLinkNamespaceDto).GetProperty(nameof(ShortLinkNamespace.OwnerUserId)).ShouldBeNull();
        typeof(ShortLinkNamespaceDto).GetProperty(nameof(ShortLinkNamespace.TenantId)).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" Campaign")]
    [InlineData("Campaign ")]
    [InlineData("Campaign\n")]
    [InlineData("a/b")]
    [InlineData("a.b")]
    [InlineData("含中文")]
    public async Task Proxied_Create_Should_Reject_Invalid_Names_Before_Eligibility_Or_Persistence(string? name)
    {
        await Should.ThrowAsync<AbpValidationException>(() =>
            CreateNamespaceAsync(name!));
        await _eligibility.DidNotReceive().EnsureEligibleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        (await NamespaceCountAsync()).ShouldBe(0);
        _locks.Acquisitions.ShouldBe(0);
    }

    [Fact]
    public async Task Proxied_Create_Should_Enforce_Name_Length_And_Required_Origin()
    {
        await Should.ThrowAsync<AbpValidationException>(() =>
            CreateNamespaceAsync(new string('A', 65)));
        await Should.ThrowAsync<AbpValidationException>(() =>
            CreateNamespaceAsync("Valid", ""));
        var valid = await CreateNamespaceAsync(new string('A', 64));
        valid.Name.Length.ShouldBe(64);
        (await NamespaceCountAsync()).ShouldBe(1);
    }

    [Theory]
    [InlineData("API")]
    [InlineData("Admin")]
    [InlineData("_FRAMEWORK")]
    [InlineData("Subscriptions")]
    [InlineData("Identity")]
    [InlineData("TenantManagement")]
    [InlineData("SettingManagement")]
    [InlineData("FeatureManagement")]
    [InlineData("AbpPermissionManagement")]
    [InlineData("Signout-Oidc")]
    public async Task Protected_Prefixes_Should_Be_Domain_Errors_Without_Writes(string name)
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync(name));
        exception.Code.ShouldBe(ShortLinkErrorCodes.ReservedNamespace);
        exception.Data["Namespace"].ShouldBe(name);
        (await NamespaceCountAsync()).ShouldBe(0);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(ShortLinkPublicPermissions.Namespaces.Default)]
    [InlineData(ShortLinkPublicPermissions.Namespaces.Create)]
    [InlineData(ShortLinkPublicPermissions.Namespaces.Delete)]
    public async Task Namespace_Permissions_Should_Be_Enforced_By_The_Proxy(string permission)
    {
        var entity = await CreateNamespaceAsync("PermissionTest");
        _eligibility.ClearReceivedCalls();
        _deniedPermission = permission;

        if (permission == ShortLinkPublicPermissions.Namespaces.Default)
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _namespaces.GetListAsync());
        }
        else if (permission == ShortLinkPublicPermissions.Namespaces.Create)
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => CreateNamespaceAsync("Denied"));
        }
        else
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => DeleteNamespaceAsync(entity));
        }

        _deniedPermission = null;
        await _eligibility.DidNotReceive().EnsureEligibleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        (await _namespaces.GetListAsync()).Items.ShouldHaveSingleItem().Id.ShouldBe(entity.Id);
    }

    [Fact]
    public async Task Anonymous_Namespace_Requests_Should_Not_Reach_Eligibility_Or_Repositories()
    {
        using var anonymous = GetRequiredService<ICurrentPrincipalAccessor>()
            .Change(new ClaimsPrincipal(new ClaimsIdentity()));
        await Should.ThrowAsync<AbpAuthorizationException>(() => _namespaces.GetListAsync());
        await Should.ThrowAsync<AbpAuthorizationException>(() => CreateNamespaceAsync("Anonymous"));
        await Should.ThrowAsync<AbpAuthorizationException>(() => _namespaces.DeleteAsync(
            Guid.NewGuid(), new DeleteShortLinkNamespaceDto { ConcurrencyStamp = "valid-stamp" }));
        await _eligibility.DidNotReceive().EnsureEligibleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _locks.Acquisitions.ShouldBe(0);
    }

    [Fact]
    public async Task Namespace_Creation_Should_Check_The_Current_Owners_Eligibility()
    {
        _eligibility.EnsureEligibleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AbpException("Test eligibility rejection.")));

        await Should.ThrowAsync<AbpException>(() => CreateNamespaceAsync("Ineligible"));
        await _eligibility.Received(1).EnsureEligibleAsync(_currentUser.GetId(), Arg.Any<CancellationToken>());
        (await NamespaceCountAsync()).ShouldBe(0);
        _locks.Acquisitions.ShouldBe(0);
    }

    [Fact]
    public async Task Namespace_Names_Should_Be_Case_Sensitive_But_Unique_For_The_Normalized_Origin()
    {
        var first = await CreateNamespaceAsync("Campaign");
        var second = await CreateNamespaceAsync("campaign");
        var duplicate = await Should.ThrowAsync<BusinessException>(() =>
            CreateNamespaceAsync("Campaign", "HTTPS://GO.EXAMPLE.TEST:443/"));
        duplicate.Code.ShouldBe(ShortLinkErrorCodes.NamespaceAlreadyExists);
        duplicate.Data["Namespace"].ShouldBe("Campaign");
        first.Id.ShouldNotBe(second.Id);
        (await NamespaceCountAsync()).ShouldBe(2);

        using (ChangeUser(Guid.NewGuid()))
        {
            (await _namespaces.GetListAsync()).Items.ShouldBeEmpty();
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("Campaign"))).Code
                .ShouldBe(ShortLinkErrorCodes.NamespaceAlreadyExists);
        }
    }

    [Fact]
    public async Task Namespace_Lists_Selection_And_Deletion_Should_Enforce_Owner_And_Tenant()
    {
        var entity = await CreateNamespaceAsync("Owned");
        using (ChangeUser(Guid.NewGuid()))
        {
            (await _namespaces.GetListAsync()).Items.ShouldBeEmpty();
            (await _links.GetCapabilitiesAsync()).UsedNamespaces.ShouldBe(0);
            (await Should.ThrowAsync<BusinessException>(() =>
                CreateLinkAsync(namespaceId: entity.Id))).Code.ShouldBe(ShortLinkErrorCodes.NamespaceAccessDenied);
            (await Should.ThrowAsync<BusinessException>(() =>
                DeleteNamespaceAsync(entity))).Code.ShouldBe(ShortLinkErrorCodes.NamespaceAccessDenied);
        }

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await CreateDomainAsync(Origin);
            (await _namespaces.GetListAsync()).Items.ShouldBeEmpty();
            (await _links.GetCapabilitiesAsync()).UsedNamespaces.ShouldBe(0);
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync(entity.Name))).Code
                .ShouldBe(ShortLinkErrorCodes.NamespaceAlreadyExists);
            (await Should.ThrowAsync<BusinessException>(() =>
                CreateLinkAsync(namespaceId: entity.Id))).Code.ShouldBe(ShortLinkErrorCodes.NamespaceAccessDenied);
            using (GetRequiredService<IDataFilter<IMultiTenant>>().Disable())
            {
                (await Should.ThrowAsync<BusinessException>(() =>
                    CreateLinkAsync(namespaceId: entity.Id))).Code.ShouldBe(ShortLinkErrorCodes.NamespaceAccessDenied);
                (await Should.ThrowAsync<BusinessException>(() =>
                    DeleteNamespaceAsync(entity))).Code.ShouldBe(ShortLinkErrorCodes.NamespaceAccessDenied);
            }
            await CreateNamespaceAsync("TenantOwned");
            (await NamespaceCountAsync()).ShouldBe(1);
        }

        (await _namespaces.GetListAsync()).Items.ShouldHaveSingleItem().Id.ShouldBe(entity.Id);
    }

    [Fact]
    public async Task Finite_Namespace_Quota_Should_Be_Isolated_By_Owner_And_Tenant()
    {
        _capabilities.NamespaceQuota = ShortLinkQuota.Limited(1);
        var hostNamespace = await CreateNamespaceAsync("HostOwned");
        using (ChangeUser(Guid.NewGuid()))
        {
            await CreateNamespaceAsync("OtherOwner");
            (await _links.GetCapabilitiesAsync()).UsedNamespaces.ShouldBe(1);
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("OtherOwnerOverflow"))).Code
                .ShouldBe(ShortLinkErrorCodes.NamespaceQuotaExceeded);
        }
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await CreateDomainAsync(Origin);
            await CreateNamespaceAsync("TenantOwned");
            (await _links.GetCapabilitiesAsync()).UsedNamespaces.ShouldBe(1);
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("TenantOverflow"))).Code
                .ShouldBe(ShortLinkErrorCodes.NamespaceQuotaExceeded);
        }

        (await _namespaces.GetListAsync()).Items.ShouldHaveSingleItem().Id.ShouldBe(hostNamespace.Id);
        (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("HostOverflow"))).Code
            .ShouldBe(ShortLinkErrorCodes.NamespaceQuotaExceeded);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Namespace_Creation_And_Selection_Should_Respect_Domain_Entitlements_And_Exact_Domain_Identity()
    {
        var extra = await CreateDomainAsync("https://alt.example.test");
        _capabilities.DomainAccess = ShortLinkDomainAccess.Restricted([]);
        await CreateNamespaceAsync("DefaultAllowed");
        (await Should.ThrowAsync<BusinessException>(() =>
            CreateNamespaceAsync("ExtraDenied", extra.Origin))).Code.ShouldBe(ShortLinkErrorCodes.DomainAccessDenied);

        _capabilities.DomainAccess = ShortLinkDomainAccess.Restricted([extra.Origin]);
        var selected = await CreateNamespaceAsync("ExtraAllowed", extra.Origin);
        (await Should.ThrowAsync<BusinessException>(() =>
            CreateLinkAsync(namespaceId: selected.Id))).Code.ShouldBe(ShortLinkErrorCodes.NamespaceAccessDenied);
        var link = await CreateLinkAsync(origin: extra.Origin, namespaceId: selected.Id);
        link.Namespace.ShouldBe(selected.Name);
        link.NamespaceId.ShouldBe(selected.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            var domains = GetRequiredService<IShortLinkDomainRepository>();
            var current = await domains.GetAsync(extra.Id);
            current.Disable();
            await domains.UpdateAsync(current, autoSave: true);
        });
        (await Should.ThrowAsync<BusinessException>(() =>
            CreateNamespaceAsync("Disabled", extra.Origin))).Code.ShouldBe(ShortLinkErrorCodes.DomainAccessDenied);
        (await Should.ThrowAsync<BusinessException>(() =>
            CreateLinkAsync(origin: extra.Origin, namespaceId: selected.Id))).Code
            .ShouldBe(ShortLinkErrorCodes.DomainAccessDenied);
        (await ResolveAsync(link)).Status.ShouldBe(ShortLinkResolutionStatus.Found);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Denied_And_Zero_Namespace_Quotas_Should_Mean_Namespaces_Are_Not_Granted(bool quotaGranted)
    {
        _capabilities.NamespaceQuota = quotaGranted ? ShortLinkQuota.Limited(0) : ShortLinkQuota.Denied;
        (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("Denied"))).Code
            .ShouldBe(ShortLinkErrorCodes.NamespacesNotGranted);
        (await NamespaceCountAsync()).ShouldBe(0);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Namespace_Quota_Downgrade_Should_Block_New_Names_But_Not_Using_Existing_Names()
    {
        var selected = await CreateNamespaceAsync("Existing");
        var unused = await CreateNamespaceAsync("Unused");
        _capabilities.RootLinkQuota = ShortLinkQuota.Denied;
        _capabilities.NamespaceQuota = ShortLinkQuota.Limited(1);
        (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("Downgraded"))).Code
            .ShouldBe(ShortLinkErrorCodes.NamespaceQuotaExceeded);
        var link = await CreateLinkAsync(namespaceId: selected.Id);
        link.NamespaceId.ShouldBe(selected.Id);

        foreach (var quota in new[] { ShortLinkQuota.Denied, ShortLinkQuota.Limited(0) })
        {
            _capabilities.NamespaceQuota = quota;
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("Downgraded"))).Code
                .ShouldBe(ShortLinkErrorCodes.NamespacesNotGranted);
            (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync(namespaceId: selected.Id))).Code
                .ShouldBe(ShortLinkErrorCodes.NamespacesNotGranted);
        }

        var defaultLink = await CreateLinkAsync();
        defaultLink.Namespace.ShouldBe("s");
        _capabilities.RootLinkQuota = ShortLinkQuota.Unlimited;
        (await CreateLinkAsync(useRootPath: true)).Namespace.ShouldBe(string.Empty);
        var updated = await _links.UpdateAsync(link.Id, new UpdateShortLinkDto
        {
            TargetUrl = "https://example.com/downgraded",
            ConcurrencyStamp = link.ConcurrencyStamp
        });
        updated.NamespaceId.ShouldBe(selected.Id);
        (await ResolveAsync(updated)).TargetUrl.ShouldBe(updated.TargetUrl);
        await DeleteNamespaceAsync(unused);
        (await _namespaces.GetListAsync()).Items.ShouldHaveSingleItem().Id.ShouldBe(selected.Id);
    }

    [Fact]
    public async Task Capabilities_Should_Report_Namespace_And_Root_Usage_With_Denied_Zero_Finite_Overage_And_Unlimited()
    {
        var first = await CreateNamespaceAsync("First");
        await CreateNamespaceAsync("Second");
        var root = await CreateLinkAsync(useRootPath: true);
        await CreateLinkAsync(useRootPath: true);
        await CreateLinkAsync();

        foreach (var limit in new[] { 0L, 1L, 2L, 5L, long.MaxValue })
        {
            _capabilities.NamespaceQuota = ShortLinkQuota.Limited(limit);
            _capabilities.RootLinkQuota = ShortLinkQuota.Limited(limit);
            var result = await _links.GetCapabilitiesAsync();
            result.DefaultNamespace.ShouldBe("s");
            result.NamespacesEnabled.ShouldBe(limit > 0);
            result.UsedNamespaces.ShouldBe(2);
            result.UsedRootLinks.ShouldBe(2);
            result.UsedLinks.ShouldBe(3);
            result.IsNamespaceQuotaGranted.ShouldBeTrue();
            result.IsNamespaceUnlimited.ShouldBeFalse();
            result.MaxNamespaces.ShouldBe(limit);
            result.RemainingNamespaces.ShouldBe(Math.Max(0, limit - 2));
            result.CanCreateNamespace.ShouldBe(limit > 2);
            result.IsRootLinkQuotaGranted.ShouldBeTrue();
            result.IsRootLinkUnlimited.ShouldBeFalse();
            result.MaxRootLinks.ShouldBe(limit);
            result.RemainingRootLinks.ShouldBe(Math.Max(0, limit - 2));
            result.CanCreateRootLink.ShouldBe(limit > 2);
        }

        _capabilities.NamespaceQuota = ShortLinkQuota.Denied;
        _capabilities.RootLinkQuota = ShortLinkQuota.Denied;
        var denied = await _links.GetCapabilitiesAsync();
        denied.NamespacesEnabled.ShouldBeFalse();
        denied.IsNamespaceQuotaGranted.ShouldBeFalse();
        denied.IsNamespaceUnlimited.ShouldBeFalse();
        denied.MaxNamespaces.ShouldBeNull();
        denied.RemainingNamespaces.ShouldBe(0);
        denied.CanCreateNamespace.ShouldBeFalse();
        denied.IsRootLinkQuotaGranted.ShouldBeFalse();
        denied.IsRootLinkUnlimited.ShouldBeFalse();
        denied.MaxRootLinks.ShouldBeNull();
        denied.RemainingRootLinks.ShouldBe(0);
        denied.CanCreateRootLink.ShouldBeFalse();

        _capabilities.NamespaceQuota = ShortLinkQuota.Unlimited;
        _capabilities.RootLinkQuota = ShortLinkQuota.Unlimited;
        var unlimited = await _links.GetCapabilitiesAsync();
        unlimited.NamespacesEnabled.ShouldBeTrue();
        unlimited.IsNamespaceUnlimited.ShouldBeTrue();
        unlimited.MaxNamespaces.ShouldBeNull();
        unlimited.RemainingNamespaces.ShouldBeNull();
        unlimited.CanCreateNamespace.ShouldBeTrue();
        unlimited.IsRootLinkUnlimited.ShouldBeTrue();
        unlimited.MaxRootLinks.ShouldBeNull();
        unlimited.RemainingRootLinks.ShouldBeNull();
        unlimited.CanCreateRootLink.ShouldBeTrue();

        _capabilities.NamespaceQuota = ShortLinkQuota.Limited(0);
        var featureOff = await _links.GetCapabilitiesAsync();
        featureOff.NamespacesEnabled.ShouldBeFalse();
        featureOff.UsedNamespaces.ShouldBe(2);
        featureOff.CanCreateNamespace.ShouldBeFalse();
        featureOff.CanCreateRootLink.ShouldBeTrue();
        await DeleteNamespaceAsync(first);
        await DeleteLinkAsync(admin: false, root.Id, root.ConcurrencyStamp);
        var released = await _links.GetCapabilitiesAsync();
        released.UsedNamespaces.ShouldBe(1);
        released.UsedRootLinks.ShouldBe(1);
        released.UsedLinks.ShouldBe(2);
    }

    [Fact]
    public async Task Last_Namespace_Slot_Should_Be_Claimed_Once_By_Independent_Proxied_Scopes()
    {
        _capabilities.NamespaceQuota = ShortLinkQuota.Limited(1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contenders = Enumerable.Range(0, 6).Select(index => Task.Run(async () =>
        {
            await start.Task;
            using var scope = ServiceProvider.CreateScope();
            var api = scope.ServiceProvider.GetRequiredService<IShortLinkNamespaceAppService>();
            try
            {
                await api.CreateAsync(new CreateShortLinkNamespaceDto { Origin = Origin, Name = $"Candidate{index}" });
                return true;
            }
            catch (BusinessException exception) when (exception.Code == ShortLinkErrorCodes.NamespaceQuotaExceeded)
            {
                return false;
            }
        })).ToArray();
        start.SetResult();

        (await Task.WhenAll(contenders)).Count(success => success).ShouldBe(1);
        (await NamespaceCountAsync()).ShouldBe(1);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_Namespaces_Should_Not_Bypass_Total_Or_Custom_Final_Slot_Quotas(bool customQuota)
    {
        var selected = await CreateNamespaceAsync("Selected");
        _capabilities.Quota = customQuota ? ShortLinkQuota.Unlimited : ShortLinkQuota.Limited(1);
        _capabilities.CustomCodeQuota = customQuota ? ShortLinkQuota.Limited(1) : ShortLinkQuota.Unlimited;
        _capabilities.RootLinkQuota = ShortLinkQuota.Denied;
        var errorCode = customQuota ? ShortLinkErrorCodes.CustomCodeQuotaExceeded : ShortLinkErrorCodes.LinkQuotaExceeded;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contenders = Enumerable.Range(0, 6).Select(index => Task.Run(async () =>
        {
            await start.Task;
            using var scope = ServiceProvider.CreateScope();
            var api = scope.ServiceProvider.GetRequiredService<IShortLinkAppService>();
            try
            {
                await api.CreateAsync(new CreateShortLinkDto
                {
                    Origin = Origin,
                    NamespaceId = selected.Id,
                    TargetUrl = "https://example.com/",
                    CustomCode = $"Candidate{index}"
                });
                return true;
            }
            catch (BusinessException exception) when (exception.Code == errorCode)
            {
                return false;
            }
        })).ToArray();
        start.SetResult();

        (await Task.WhenAll(contenders)).Count(success => success).ShouldBe(1);
        (await WithUnitOfWorkAsync(() => _repository.GetCountByOwnerAsync(_currentUser.GetId()))).ShouldBe(1);
        (await _links.GetCapabilitiesAsync()).UsedRootLinks.ShouldBe(0);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Namespace_Quota_Failure_Should_Roll_Back_Names_And_Links_In_A_Joined_Transaction()
    {
        _capabilities.NamespaceQuota = ShortLinkQuota.Limited(1);
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            var selected = await CreateNamespaceAsync("Rollback");
            await CreateLinkAsync("RollbackCode", namespaceId: selected.Id);
            _locks.ActiveCount.ShouldBe(4);
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("Overflow"))).Code
                .ShouldBe(ShortLinkErrorCodes.NamespaceQuotaExceeded);
        }

        (await NamespaceCountAsync()).ShouldBe(0);
        (await WithUnitOfWorkAsync(() => _repository.GetCountByOwnerAsync(_currentUser.GetId()))).ShouldBe(0);
        _locks.ActiveCount.ShouldBe(0);
        var replacement = await CreateNamespaceAsync("Rollback");
        await CreateLinkAsync("RollbackCode", namespaceId: replacement.Id);
    }

    [Fact]
    public async Task Namespace_Manager_Should_Reject_Nontransactional_Cancelled_And_Unlocked_Writes()
    {
        var manager = GetRequiredService<ShortLinkNamespaceManager>();
        using (var unit = _units.Begin(requiresNew: true, isTransactional: false))
        {
            await Should.ThrowAsync<AbpException>(() =>
                manager.CreateAndSaveAsync(_currentUser.GetId(), Origin, "Nontransactional"));
        }
        await Should.ThrowAsync<OperationCanceledException>(() =>
            manager.CreateAndSaveAsync(_currentUser.GetId(), Origin, "Cancelled", new CancellationToken(true)));
        _locks.FailAcquisition = true;
        (await Should.ThrowAsync<BusinessException>(() =>
            manager.CreateAndSaveAsync(_currentUser.GetId(), Origin, "Unlocked"))).Code
            .ShouldBe(ShortLinkErrorCodes.NamespaceLockUnavailable);
        _locks.FailAcquisition = false;
        (await NamespaceCountAsync()).ShouldBe(0);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Namespace_Creation_Leases_Should_Be_Reused_And_Held_Until_Transaction_Completion()
    {
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await CreateNamespaceAsync("First");
            await CreateNamespaceAsync("Second");
            _locks.Acquisitions.ShouldBe(3);
            _locks.ActiveCount.ShouldBe(3);
            await unit.CompleteAsync();
        }
        _locks.ActiveCount.ShouldBe(0);
        (await NamespaceCountAsync()).ShouldBe(2);
    }

    [Theory]
    [InlineData("domain")]
    [InlineData("owner")]
    [InlineData("selected-link")]
    [InlineData("namespace-delete")]
    public async Task Ambient_Transactions_Should_Reject_New_Global_Configuration_After_Higher_Rank_Mutations(
        string firstMutation)
    {
        var selected = firstMutation is "selected-link" or "namespace-delete"
            ? await CreateNamespaceAsync("ExistingForUpgrade")
            : null;
        _locks.ResetAcquisitions();
        var attempts = new List<string>();
        _locks.OnAttempt = attempts.Add;
        try
        {
            using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
            {
                switch (firstMutation)
                {
                    case "domain":
                        await GetRequiredService<ShortLinkDomainConfigurationLock>()
                            .AcquireAsync(CancellationToken.None);
                        await CreateDomainAsync("https://upgrade-domain.example.test");
                        break;
                    case "owner":
                        await CreateLinkAsync("UpgradeLink");
                        break;
                    case "selected-link":
                        await CreateLinkAsync("UpgradeLink", namespaceId: selected!.Id);
                        break;
                    case "namespace-delete":
                        await DeleteNamespaceAsync(selected!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(firstMutation));
                }
                var retained = _locks.ActiveCount;
                retained.ShouldBeGreaterThan(0);
                var acquired = _locks.Acquisitions;
                var attempted = attempts.Count;

                (await Should.ThrowAsync<AbpException>(() => CreateNamespaceAsync("ForbiddenUpgrade")))
                    .ShouldBeOfType<AbpException>();
                attempts.Count.ShouldBe(attempted);
                _locks.Acquisitions.ShouldBe(acquired);
                _locks.ActiveCount.ShouldBe(retained);
                attempts.ShouldNotContain("ShortLink:NamespaceConfiguration");
            }
        }
        finally
        {
            _locks.OnAttempt = null;
        }

        _locks.ActiveCount.ShouldBe(0);
        (await NamespaceCountAsync()).ShouldBe(selected is null ? 0 : 1);
        (await _links.GetCapabilitiesAsync()).UsedLinks.ShouldBe(0);
        (await WithUnitOfWorkAsync(() => GetRequiredService<IShortLinkDomainRepository>()
            .FindByOriginAsync("https://upgrade-domain.example.test"))).ShouldBeNull();
    }

    [Theory]
    [InlineData("active")]
    [InlineData("disabled")]
    [InlineData("expired")]
    [InlineData("deleted")]
    public async Task Every_Retained_Link_Reference_Should_Prevent_Namespace_Deletion(string state)
    {
        var selected = await CreateNamespaceAsync("Referenced");
        var link = await CreateLinkAsync(
            namespaceId: selected.Id,
            expiresAt: state == "expired" ? DateTime.UtcNow.AddDays(-1) : null);
        if (state == "disabled")
        {
            await _links.SetStatusAsync(link.Id, new SetShortLinkStatusDto
            {
                Status = ShortLinkStatus.Disabled,
                ConcurrencyStamp = link.ConcurrencyStamp
            });
        }
        else if (state == "deleted")
        {
            await DeleteLinkAsync(admin: false, link.Id, link.ConcurrencyStamp);
        }

        GetRequiredService<IDataFilter<ISoftDelete>>().IsEnabled.ShouldBeTrue();
        var exception = await Should.ThrowAsync<BusinessException>(() => DeleteNamespaceAsync(selected));
        exception.Code.ShouldBe(ShortLinkErrorCodes.NamespaceInUse);
        exception.Data["Namespace"].ShouldBe(selected.Name);
        (await NamespaceCountAsync()).ShouldBe(1);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Proxied_Delete_Should_Require_A_Concurrency_Stamp(string? stamp)
    {
        var selected = await CreateNamespaceAsync("Versioned");
        _locks.ResetAcquisitions();
        await Should.ThrowAsync<AbpValidationException>(() => _namespaces.DeleteAsync(
            selected.Id, new DeleteShortLinkNamespaceDto { ConcurrencyStamp = stamp! }));
        (await NamespaceCountAsync()).ShouldBe(1);
        _locks.Acquisitions.ShouldBe(0);
    }

    [Fact]
    public async Task Namespace_Delete_Should_Preserve_The_Two_Writer_Concurrency_Contract()
    {
        var firstRead = await CreateNamespaceAsync("Versioned");
        var secondRead = (await _namespaces.GetListAsync()).Items.Single();
        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await _namespaceRepository.GetAsync(firstRead.Id);
            entity.SetProperty("Writer", "first");
            await _namespaceRepository.UpdateAsync(entity, autoSave: true);
        });
        var updated = (await _namespaces.GetListAsync()).Items.Single();
        updated.ConcurrencyStamp.ShouldNotBe(firstRead.ConcurrencyStamp);

        await Should.ThrowAsync<AbpDbConcurrencyException>(() => DeleteNamespaceAsync(secondRead));
        (await _namespaces.GetListAsync()).Items.ShouldHaveSingleItem().ConcurrencyStamp
            .ShouldBe(updated.ConcurrencyStamp);
        await DeleteNamespaceAsync(updated);
        (await NamespaceCountAsync()).ShouldBe(0);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Link_Delete_Should_Preserve_The_Two_Writer_Concurrency_Contract(bool admin)
    {
        var firstRead = await CreateLinkAsync("VersionedLink");
        foreach (var missing in new[] { null, string.Empty, " " })
        {
            await Should.ThrowAsync<AbpValidationException>(() =>
                DeleteLinkAsync(admin, firstRead.Id, missing!));
        }

        var updated = await _links.UpdateAsync(firstRead.Id, new UpdateShortLinkDto
        {
            TargetUrl = "https://example.com/second-writer",
            ConcurrencyStamp = firstRead.ConcurrencyStamp
        });
        updated.ConcurrencyStamp.ShouldNotBe(firstRead.ConcurrencyStamp);

        await Should.ThrowAsync<AbpDbConcurrencyException>(() =>
            DeleteLinkAsync(admin, firstRead.Id, firstRead.ConcurrencyStamp));
        (await _links.GetAsync(firstRead.Id)).ConcurrencyStamp.ShouldBe(updated.ConcurrencyStamp);
        (await ResolveAsync(updated)).Status.ShouldBe(ShortLinkResolutionStatus.Found);

        await DeleteLinkAsync(admin, firstRead.Id, updated.ConcurrencyStamp);
        (await ResolveAsync(updated)).Status.ShouldBe(ShortLinkResolutionStatus.Gone);
    }

    [Fact]
    public async Task Namespace_Delete_Should_Hold_Leases_Through_Rollback_And_Not_Release_Quota_Before_Commit()
    {
        _capabilities.NamespaceQuota = ShortLinkQuota.Limited(1);
        var selected = await CreateNamespaceAsync("RollbackDelete");
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await DeleteNamespaceAsync(selected);
            _locks.ActiveCount.ShouldBe(2);
            await unit.RollbackAsync();
            _locks.ActiveCount.ShouldBe(2);
        }
        _locks.ActiveCount.ShouldBe(0);
        (await NamespaceCountAsync()).ShouldBe(1);
        (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync("StillFull"))).Code
            .ShouldBe(ShortLinkErrorCodes.NamespaceQuotaExceeded);
        await DeleteNamespaceAsync((await _namespaces.GetListAsync()).Items.Single());
        await CreateNamespaceAsync("Released");
    }

    [Fact]
    public async Task Namespace_Delete_Should_Wait_For_A_Selected_Link_Transaction_And_Then_Observe_The_Reference()
    {
        var selected = await CreateNamespaceAsync("Racing");
        var owner = _currentUser.GetId();
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> deleting;
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await CreateLinkAsync(namespaceId: selected.Id);
            _locks.ActiveCount.ShouldBe(2);
            _locks.OnAttempt = key =>
            {
                if (key == $"ShortLink:Creation:host:{owner:N}")
                {
                    attempted.TrySetResult();
                }
            };
            using (ExecutionContext.SuppressFlow())
            {
                deleting = Task.Run(async () =>
                {
                    using var scope = ServiceProvider.CreateScope();
                    using var principal = scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>()
                        .Change(CreatePrincipal(owner));
                    var api = scope.ServiceProvider.GetRequiredService<IShortLinkNamespaceAppService>();
                    try
                    {
                        await api.DeleteAsync(selected.Id, new DeleteShortLinkNamespaceDto
                        {
                            ConcurrencyStamp = selected.ConcurrencyStamp
                        });
                        return true;
                    }
                    catch (BusinessException exception) when (exception.Code == ShortLinkErrorCodes.NamespaceInUse)
                    {
                        return false;
                    }
                });
            }
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            deleting.IsCompleted.ShouldBeFalse();
            await unit.CompleteAsync();
        }

        (await deleting.WaitAsync(TimeSpan.FromSeconds(15))).ShouldBeFalse();
        _locks.OnAttempt = null;
        (await NamespaceCountAsync()).ShouldBe(1);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task An_Empty_Namespace_Should_Prevent_Deleting_Its_Domain_Until_The_Namespace_Is_Deleted()
    {
        var extra = await CreateDomainAsync("https://namespace-domain.example.test");
        var selected = await CreateNamespaceAsync("OnlyNamespace", extra.Origin);
        var domains = GetRequiredService<DomainAdministration>();
        var deleteDomain = new DeleteShortLinkDomainDto { ConcurrencyStamp = extra.ConcurrencyStamp };
        (await Should.ThrowAsync<BusinessException>(() => domains.DeleteAsync(extra.Id, deleteDomain))).Code
            .ShouldBe(ShortLinkErrorCodes.DomainInUse);
        await DeleteNamespaceAsync(selected);
        await domains.DeleteAsync(extra.Id, deleteDomain);
        (await _namespaces.GetListAsync()).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Referencing_A_Domain_Or_Namespace_Should_Not_Refresh_Its_Concurrency_Stamp()
    {
        var domain = await CreateDomainAsync("https://stamp-domain.example.test");
        var selected = await CreateNamespaceAsync("StampNamespace", domain.Origin);
        var link = await CreateLinkAsync(origin: domain.Origin, namespaceId: selected.Id);
        await DeleteLinkAsync(false, link.Id, link.ConcurrencyStamp);

        var domains = GetRequiredService<IShortLinkDomainRepository>();
        (await WithUnitOfWorkAsync(() => domains.GetAsync(domain.Id))).ConcurrencyStamp
            .ShouldBe(domain.ConcurrencyStamp);
        (await WithUnitOfWorkAsync(() => _namespaceRepository.GetAsync(selected.Id))).ConcurrencyStamp
            .ShouldBe(selected.ConcurrencyStamp);
    }

    [Fact]
    public async Task Default_Setting_Should_Preserve_Exact_Case_And_Every_Historical_Reservation()
    {
        var original = await CreateLinkAsync("Historical");
        var upper = await CreateNamespaceAsync("S");
        var input = await _settings.GetAsync();
        input.DefaultNamespace = "S";
        (await Should.ThrowAsync<BusinessException>(() => _settings.UpdateAsync(input))).Code
            .ShouldBe(ShortLinkErrorCodes.NamespaceAlreadyExists);
        (await IsReservedAsync("S")).ShouldBeFalse();
        (await _settings.GetAsync()).DefaultNamespace.ShouldBe("s");
        await DeleteNamespaceAsync(upper);

        (await _settings.UpdateAsync(input)).DefaultNamespace.ShouldBe("S");
        _settingStore.CommittedValue(ShortLinkSettings.DefaultNamespace).ShouldBe("S");
        (await GetRequiredService<ISettingManager>().GetOrNullAsync(
            ShortLinkSettings.DefaultNamespace, GlobalSettingValueProvider.ProviderName, null!, fallback: false))
            .ShouldBe("S");
        var changed = await CreateLinkAsync("Historical");
        changed.Namespace.ShouldBe("S");
        changed.NamespaceId.ShouldBeNull();
        changed.ShortUrl.ShouldBe($"{Origin}/S/Historical");
        (await _links.GetAsync(original.Id)).ShortUrl.ShouldBe($"{Origin}/s/Historical");
        (await ResolveAsync(original)).ShortLinkId.ShouldBe(original.Id);
        (await ResolveAsync(changed)).ShortLinkId.ShouldBe(changed.Id);

        input.DefaultNamespace = "Next_Default";
        await _settings.UpdateAsync(input);
        foreach (var name in new[] { "s", "S", "Next_Default" })
        {
            (await IsReservedAsync(name)).ShouldBeTrue();
            (await Should.ThrowAsync<BusinessException>(() => CreateNamespaceAsync(name))).Code
                .ShouldBe(ShortLinkErrorCodes.ReservedNamespace);
        }
        (await _links.GetCapabilitiesAsync()).DefaultNamespace.ShouldBe("Next_Default");
    }

    [Fact]
    public async Task Default_Setting_Should_Reject_A_Namespace_Conflict_In_Another_Tenant_And_Origin()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var domain = await CreateDomainAsync("https://tenant-settings.example.test");
            await CreateNamespaceAsync("GloballyTaken", domain.Origin);
        }
        var input = await _settings.GetAsync();
        input.DefaultNamespace = "GloballyTaken";

        var exception = await Should.ThrowAsync<BusinessException>(() => _settings.UpdateAsync(input));
        exception.Code.ShouldBe(ShortLinkErrorCodes.NamespaceAlreadyExists);
        (await IsReservedAsync("GloballyTaken")).ShouldBeFalse();
        (await _settings.GetAsync()).DefaultNamespace.ShouldBe("s");
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_Failed_Settings_Write_Should_Roll_Back_The_Reservation_And_Earlier_Setting_Write()
    {
        var input = await _settings.GetAsync();
        input.DefaultNamespace = "FailedDefault";
        input.MaxLinksPerUser++;
        _settingStore.FailOnSetting = ShortLinkSettings.MaxLinksPerUser;

        await Should.ThrowAsync<AbpException>(() => _settings.UpdateAsync(input));
        (await IsReservedAsync("FailedDefault")).ShouldBeFalse();
        _settingStore.CommittedValue(ShortLinkSettings.DefaultNamespace).ShouldBeNull();
        (await _settings.GetAsync()).DefaultNamespace.ShouldBe("s");
        _locks.ActiveCount.ShouldBe(0);

        _settingStore.FailOnSetting = null;
        (await _settings.UpdateAsync(input)).DefaultNamespace.ShouldBe("FailedDefault");
        (await IsReservedAsync("FailedDefault")).ShouldBeTrue();
    }

    [Fact]
    public async Task A_Joined_Settings_Transaction_Should_Not_Publish_A_Rolled_Back_Default_Or_Reservation()
    {
        var input = await _settings.GetAsync();
        input.DefaultNamespace = "RolledBackDefault";
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            (await _settings.UpdateAsync(input)).DefaultNamespace.ShouldBe("RolledBackDefault");
            _settingStore.CommittedValue(ShortLinkSettings.DefaultNamespace).ShouldBeNull();
            _locks.ActiveCount.ShouldBe(1);
            await unit.RollbackAsync();
        }

        (await _settings.GetAsync()).DefaultNamespace.ShouldBe("s");
        (await IsReservedAsync("RolledBackDefault")).ShouldBeFalse();
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_Default_Reservation_And_User_Namespace_Race_Should_Have_Exactly_One_Winner()
    {
        var input = await _settings.GetAsync();
        input.DefaultNamespace = "ReservedRace";
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reserve = Task.Run(async () =>
        {
            await start.Task;
            using var scope = ServiceProvider.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IShortLinkSettingsAppService>().UpdateAsync(input);
                return true;
            }
            catch (BusinessException exception) when (exception.Code == ShortLinkErrorCodes.NamespaceAlreadyExists)
            {
                return false;
            }
        });
        var create = Task.Run(async () =>
        {
            await start.Task;
            using var scope = ServiceProvider.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IShortLinkNamespaceAppService>().CreateAsync(
                    new CreateShortLinkNamespaceDto { Origin = Origin, Name = input.DefaultNamespace });
                return true;
            }
            catch (BusinessException exception) when (exception.Code == ShortLinkErrorCodes.ReservedNamespace)
            {
                return false;
            }
        });
        start.SetResult();

        (await Task.WhenAll(reserve, create)).Count(success => success).ShouldBe(1);
        (await IsReservedAsync(input.DefaultNamespace)).ShouldBe(await reserve);
        (await NamespaceCountAsync()).ShouldBe(await create ? 1 : 0);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Unreserved_Persisted_Defaults_Should_Fail_Explicitly_Instead_Of_Falling_Back()
    {
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await _settingStore.SetAsync(
                ShortLinkSettings.DefaultNamespace, "Unreserved", GlobalSettingValueProvider.ProviderName, null!);
            await unit.CompleteAsync();
        }
        var exception = await Should.ThrowAsync<BusinessException>(() => _links.GetCapabilitiesAsync());
        exception.Code.ShouldBe(ShortLinkErrorCodes.InvalidSettingValue);
        exception.Data["SettingName"].ShouldBe(ShortLinkSettings.DefaultNamespace);
        (await Should.ThrowAsync<BusinessException>(() => CreateLinkAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.InvalidSettingValue);
    }

    [Fact]
    public async Task Root_Default_And_Case_Distinct_User_Namespaces_Should_Resolve_Only_Their_Exact_Routes()
    {
        var upper = await CreateNamespaceAsync("Campaign");
        var lower = await CreateNamespaceAsync("campaign");
        var root = await CreateLinkAsync("SharedRoute", target: "https://example.com/root", useRootPath: true);
        var defaultLink = await CreateLinkAsync("SharedRoute", target: "https://example.com/default");
        var first = await CreateLinkAsync("SharedRoute", target: "https://example.com/upper", namespaceId: upper.Id);
        var second = await CreateLinkAsync("SharedRoute", target: "https://example.com/lower", namespaceId: lower.Id);
        var extra = await CreateDomainAsync("https://alt.example.test");
        var otherOrigin = await CreateLinkAsync(
            "SharedRoute", origin: extra.Origin, target: "https://example.com/other-origin", useRootPath: true);

        foreach (var link in new[] { root, defaultLink, first, second, otherOrigin })
        {
            var resolved = await ResolveAsync(link);
            resolved.ShortLinkId.ShouldBe(link.Id);
            resolved.TargetUrl.ShouldBe(link.TargetUrl);
            (await ResolveAsync(link)).TargetUrl.ShouldBe(link.TargetUrl);
            (await WithUnitOfWorkAsync(() =>
                _repository.FindByCodeAsync(link.Origin!, link.Namespace, link.Code)))!.Id.ShouldBe(link.Id);
        }
        (await _redirect.ResolveAsync(Origin, root.Code)).ShortLinkId.ShouldBe(root.Id);
        (await WithUnitOfWorkAsync(() => _repository.FindByCodeAsync(Origin, root.Code)))!.Id.ShouldBe(root.Id);
        (await _redirect.ResolveAsync(Origin, "CAMPAIGN", first.Code)).Status.ShouldBe(ShortLinkResolutionStatus.NotFound);
        (await _redirect.ResolveAsync(Origin, first.Namespace, "sharedroute")).Status
            .ShouldBe(ShortLinkResolutionStatus.NotFound);
        (await _redirect.ResolveAsync("https://wrong.example.test", first.Namespace, first.Code)).Status
            .ShouldBe(ShortLinkResolutionStatus.NotFound);
        ShortLinkResolutionCacheKey.Create("HTTPS://GO.EXAMPLE.TEST:443/", "Campaign", "SharedRoute")
            .ShouldBe($"v2|{Origin}|Campaign|SharedRoute");
        ShortLinkResolutionCacheKey.Create(Origin, root.Code)
            .ShouldBe(ShortLinkResolutionCacheKey.Create(Origin, "", root.Code));
    }

    [Fact]
    public async Task Legacy_Fallback_Should_Be_Root_Only_And_Only_On_The_Configured_Origin()
    {
        var legacy = new ShortLink(
            Guid.NewGuid(), null, _currentUser.GetId(), "LegacyOnly", "https://example.com/legacy", null, null);
        await WithUnitOfWorkAsync(() => _repository.InsertAsync(legacy, autoSave: true));
        (await _redirect.ResolveAsync(Origin, legacy.Code)).ShortLinkId.ShouldBe(legacy.Id);
        (await _redirect.ResolveAsync(Origin, "", legacy.Code)).ShortLinkId.ShouldBe(legacy.Id);
        (await _redirect.ResolveAsync(Origin, "s", legacy.Code)).Status.ShouldBe(ShortLinkResolutionStatus.NotFound);
        (await _redirect.ResolveAsync(Origin, "S", legacy.Code)).Status.ShouldBe(ShortLinkResolutionStatus.NotFound);
        (await _redirect.ResolveAsync("https://alt.example.test", legacy.Code)).Status
            .ShouldBe(ShortLinkResolutionStatus.NotFound);

        var namespaced = await CreateLinkAsync(legacy.Code, target: "https://example.com/namespaced");
        (await ResolveAsync(namespaced)).ShortLinkId.ShouldBe(namespaced.Id);
        (await _redirect.ResolveAsync(Origin, legacy.Code)).ShortLinkId.ShouldBe(legacy.Id);
        (await WithUnitOfWorkAsync(() => _repository.FindByCodeAsync(Origin, legacy.Code))).ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _repository.CodeExistsAsync(Origin, legacy.Code))).ShouldBeFalse();
        (await WithUnitOfWorkAsync(() => _repository.CodeExistsAsync(Origin, "s", legacy.Code))).ShouldBeTrue();
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a b")]
    [InlineData("s\n")]
    [InlineData("含中文")]
    public async Task Redirect_Should_Reject_Invalid_Namespace_Without_Root_Fallback(string pathNamespace)
    {
        var root = await CreateLinkAsync("ValidCode", useRootPath: true);
        (await _redirect.ResolveAsync(Origin, pathNamespace, root.Code, new RecordShortLinkVisitDto
        {
            IpAddress = "203.0.113.10"
        })).Status.ShouldBe(ShortLinkResolutionStatus.NotFound);
        (await _links.GetAsync(root.Id)).TotalVisitCount.ShouldBe(0);
    }

    [Fact]
    public async Task Protected_Codes_Should_Be_Allowed_Under_A_Namespace_But_Not_At_Root()
    {
        var link = await CreateLinkAsync("API");
        link.ShortUrl.ShouldBe($"{Origin}/s/API");
        (await ResolveAsync(link)).Status.ShouldBe(ShortLinkResolutionStatus.Found);
        (await Should.ThrowAsync<BusinessException>(() =>
            CreateLinkAsync("API", useRootPath: true))).Code.ShouldBe(ShortLinkErrorCodes.ReservedCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Proxied_Link_Create_Should_Reject_Empty_Or_Conflicting_Namespace_Selection(bool rootConflict)
    {
        await Should.ThrowAsync<AbpValidationException>(() => CreateLinkAsync(
            namespaceId: rootConflict ? Guid.NewGuid() : Guid.Empty,
            useRootPath: rootConflict));
        _locks.Acquisitions.ShouldBe(0);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("default")]
    [InlineData("user")]
    public async Task Every_Public_And_Admin_Mutation_Should_Invalidate_Only_Its_Stored_Route_And_Qr_Should_Use_It(string route)
    {
        var selected = route == "user" ? await CreateNamespaceAsync("Campaign") : null;
        var pathNamespace = route == "root" ? "" : selected?.Name ?? "s";
        var control = await CreateLinkAsync("MutationCode", useRootPath: pathNamespace == "s");
        await ResolveAsync(control);
        (await _redirect.ResolveAsync(Origin, pathNamespace, "MutationCode")).Status
            .ShouldBe(ShortLinkResolutionStatus.NotFound);
        (await _cache.GetAsync(ShortLinkResolutionCacheKey.Create(Origin, pathNamespace, "MutationCode")))!
            .Exists.ShouldBeFalse();

        var created = await CreateLinkAsync(
            "MutationCode", namespaceId: selected?.Id, useRootPath: route == "root");
        created.Namespace.ShouldBe(pathNamespace);
        created.NamespaceId.ShouldBe(selected?.Id);
        created.ShortUrl.ShouldBe(GetRequiredService<IShortLinkUrlBuilder>().Build(Origin, pathNamespace, created.Code));
        typeof(UpdateShortLinkDto).GetProperty(nameof(ShortLinkDto.Namespace)).ShouldBeNull();
        typeof(UpdateShortLinkDto).GetProperty(nameof(ShortLinkDto.NamespaceId)).ShouldBeNull();
        await AssertRouteCacheRemovedAsync(created, control);
        (await _links.GetListAsync(new GetShortLinksInput())).Items.Single(x => x.Id == created.Id)
            .ShortUrl.ShouldBe(created.ShortUrl);
        (await _admin.GetListAsync(new GetShortLinksInput())).Items.Single(x => x.Id == created.Id)
            .Namespace.ShouldBe(pathNamespace);
        (await _links.GetQrCodeAsync(created.Id)).Content.ShouldBe(CreateQrSvg(created.ShortUrl));

        await ResolveAsync(created);
        var updated = await _links.UpdateAsync(created.Id, new UpdateShortLinkDto
        {
            TargetUrl = "https://example.com/updated",
            ConcurrencyStamp = created.ConcurrencyStamp
        });
        updated.Namespace.ShouldBe(pathNamespace);
        updated.NamespaceId.ShouldBe(selected?.Id);
        await AssertRouteCacheRemovedAsync(updated, control);
        (await ResolveAsync(updated)).TargetUrl.ShouldBe(updated.TargetUrl);

        var disabled = await _links.SetStatusAsync(created.Id, new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Disabled,
            ConcurrencyStamp = updated.ConcurrencyStamp
        });
        await AssertRouteCacheRemovedAsync(disabled, control);
        (await ResolveAsync(disabled)).Status.ShouldBe(ShortLinkResolutionStatus.Gone);
        var active = await _links.SetStatusAsync(created.Id, new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Active,
            ConcurrencyStamp = disabled.ConcurrencyStamp
        });
        await AssertRouteCacheRemovedAsync(active, control);
        (await ResolveAsync(active)).Status.ShouldBe(ShortLinkResolutionStatus.Found);

        var adminDisabled = await _admin.SetStatusAsync(created.Id, new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Disabled,
            ConcurrencyStamp = active.ConcurrencyStamp
        });
        await AssertRouteCacheRemovedAsync(adminDisabled, control);
        (await ResolveAsync(adminDisabled)).Status.ShouldBe(ShortLinkResolutionStatus.Gone);
        var adminActive = await _admin.SetStatusAsync(created.Id, new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Active,
            ConcurrencyStamp = adminDisabled.ConcurrencyStamp
        });
        await AssertRouteCacheRemovedAsync(adminActive, control);
        (await ResolveAsync(adminActive)).Status.ShouldBe(ShortLinkResolutionStatus.Found);

        await DeleteLinkAsync(admin: false, created.Id, adminActive.ConcurrencyStamp);
        await AssertRouteCacheRemovedAsync(adminActive, control);
        (await ResolveAsync(adminActive)).Status.ShouldBe(ShortLinkResolutionStatus.Gone);
        var adminDeleted = await CreateLinkAsync(
            "AdminDeleted", namespaceId: selected?.Id, useRootPath: route == "root");
        await ResolveAsync(adminDeleted);
        await DeleteLinkAsync(admin: true, adminDeleted.Id, adminDeleted.ConcurrencyStamp);
        await AssertRouteCacheRemovedAsync(adminDeleted, control);
        (await ResolveAsync(adminDeleted)).Status.ShouldBe(ShortLinkResolutionStatus.Gone);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_Public_And_Admin_Mutations_Should_Invalidate_The_Root_Key_Only(bool admin)
    {
        var legacy = new ShortLink(
            Guid.NewGuid(), null, _currentUser.GetId(), "LegacyMutation", "https://example.com/legacy", null, null);
        await WithUnitOfWorkAsync(() => _repository.InsertAsync(legacy, autoSave: true));
        var control = await CreateLinkAsync(legacy.Code);
        await ResolveAsync(control);
        var current = await _links.GetAsync(legacy.Id);
        current.Namespace.ShouldBe(string.Empty);
        current.NamespaceId.ShouldBeNull();
        current.ShortUrl.ShouldBe($"{Origin}/{legacy.Code}");
        (await _links.GetQrCodeAsync(legacy.Id)).Content.ShouldBe(CreateQrSvg(current.ShortUrl));
        await ResolveAsync(current);

        if (!admin)
        {
            current = await _links.UpdateAsync(legacy.Id, new UpdateShortLinkDto
            {
                TargetUrl = "https://example.com/changed-legacy",
                ConcurrencyStamp = current.ConcurrencyStamp
            });
            await AssertRouteCacheRemovedAsync(current, control);
            (await ResolveAsync(current)).TargetUrl.ShouldBe(current.TargetUrl);
        }
        var disabled = admin
            ? await _admin.SetStatusAsync(legacy.Id, new SetShortLinkStatusDto
            {
                Status = ShortLinkStatus.Disabled, ConcurrencyStamp = current.ConcurrencyStamp
            })
            : await _links.SetStatusAsync(legacy.Id, new SetShortLinkStatusDto
            {
                Status = ShortLinkStatus.Disabled, ConcurrencyStamp = current.ConcurrencyStamp
            });
        await AssertRouteCacheRemovedAsync(disabled, control);
        (await ResolveAsync(disabled)).Status.ShouldBe(ShortLinkResolutionStatus.Gone);
        await DeleteLinkAsync(admin, legacy.Id, disabled.ConcurrencyStamp);
        await AssertRouteCacheRemovedAsync(disabled, control);
        (await _redirect.ResolveAsync(Origin, legacy.Code)).Status.ShouldBe(ShortLinkResolutionStatus.Gone);
    }

    [Fact]
    public async Task Rolled_Back_Public_Mutations_Should_Not_Invalidate_The_Committed_Route_Cache()
    {
        var link = await CreateLinkAsync("CacheRollback");
        await ResolveAsync(link);
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await _links.UpdateAsync(link.Id, new UpdateShortLinkDto
            {
                TargetUrl = "https://example.com/uncommitted",
                ConcurrencyStamp = link.ConcurrencyStamp
            });
            await unit.RollbackAsync();
        }
        var cached = await _cache.GetAsync(ShortLinkResolutionCacheKey.Create(Origin, link.Namespace, link.Code));
        cached.ShouldNotBeNull();
        cached.TargetUrl.ShouldBe(link.TargetUrl);
        (await ResolveAsync(link)).TargetUrl.ShouldBe(link.TargetUrl);
    }

    [Theory]
    [InlineData("root", false)]
    [InlineData("root", true)]
    [InlineData("default", false)]
    [InlineData("default", true)]
    [InlineData("user", false)]
    [InlineData("user", true)]
    [InlineData("legacy", false)]
    [InlineData("legacy", true)]
    public async Task Outer_Creation_Transactions_Should_Publish_Resolved_Routes_Only_On_Commit(string route, bool commit)
    {
        var selected = route == "user" ? await CreateNamespaceAsync("CacheCampaign") : null;
        ShortLinkDto link;
        string key;
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            link = await CreateCacheTestRouteAsync(route, "CacheCreation", selected?.Id);
            key = ShortLinkResolutionCacheKey.Create(Origin, link.Namespace, link.Code);
            var resolution = await ResolveAsync(link);
            resolution.Status.ShouldBe(ShortLinkResolutionStatus.Found);
            resolution.ShortLinkId.ShouldBe(link.Id);
            resolution.TargetUrl.ShouldBe(link.TargetUrl);
            (await _cache.GetAsync(key, considerUow: false)).ShouldBeNull();
            var pending = await _cache.GetAsync(key, considerUow: true);
            pending.ShouldNotBeNull();
            pending.Id.ShouldBe(link.Id);
            pending.TargetUrl.ShouldBe(link.TargetUrl);
            (await ResolveAsync(link)).ShortLinkId.ShouldBe(link.Id);

            if (commit)
            {
                await unit.CompleteAsync();
            }
            else
            {
                await unit.RollbackAsync();
            }
        }

        var published = await _cache.GetAsync(key, considerUow: false);
        if (commit)
        {
            published.ShouldNotBeNull();
            published.Exists.ShouldBeTrue();
            published.Id.ShouldBe(link.Id);
            (await ResolveAsync(link)).TargetUrl.ShouldBe(link.TargetUrl);
            (await _links.GetAsync(link.Id)).TargetUrl.ShouldBe(link.TargetUrl);
        }
        else
        {
            published.ShouldBeNull();
            (await ResolveAsync(link)).Status.ShouldBe(ShortLinkResolutionStatus.NotFound);
            (await _cache.GetAsync(key, considerUow: false))!.Exists.ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData("root", false)]
    [InlineData("root", true)]
    [InlineData("default", false)]
    [InlineData("default", true)]
    [InlineData("user", false)]
    [InlineData("user", true)]
    [InlineData("legacy", false)]
    [InlineData("legacy", true)]
    public async Task Rolled_Back_Mutations_Should_Not_Publish_Resolution_Cache_Misses(
        string route, bool delete)
    {
        var selected = route == "user" ? await CreateNamespaceAsync("CacheCampaign") : null;
        var link = await CreateCacheTestRouteAsync(route, "CacheMutation", selected?.Id);
        var key = ShortLinkResolutionCacheKey.Create(Origin, link.Namespace, link.Code);
        (await _cache.GetAsync(key, considerUow: false)).ShouldBeNull();
        const string uncommittedTarget = "https://example.com/uncommitted";

        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            if (delete)
            {
                await DeleteLinkAsync(admin: false, link.Id, link.ConcurrencyStamp);
            }
            else
            {
                await _links.UpdateAsync(link.Id, new UpdateShortLinkDto
                {
                    TargetUrl = uncommittedTarget,
                    ConcurrencyStamp = link.ConcurrencyStamp
                });
            }

            var resolution = await ResolveAsync(link);
            resolution.ShortLinkId.ShouldBe(link.Id);
            resolution.Status.ShouldBe(delete ? ShortLinkResolutionStatus.Gone : ShortLinkResolutionStatus.Found);
            if (!delete)
            {
                resolution.TargetUrl.ShouldBe(uncommittedTarget);
            }
            var pending = await _cache.GetAsync(key, considerUow: true);
            pending.ShouldNotBeNull();
            pending.Id.ShouldBe(link.Id);
            pending.IsDeleted.ShouldBe(delete);
            if (!delete)
            {
                pending.TargetUrl.ShouldBe(uncommittedTarget);
            }
            (await _cache.GetAsync(key, considerUow: false)).ShouldBeNull();
            await unit.RollbackAsync();
        }

        (await _cache.GetAsync(key, considerUow: false)).ShouldBeNull();
        var committed = await ResolveAsync(link);
        committed.Status.ShouldBe(ShortLinkResolutionStatus.Found);
        committed.TargetUrl.ShouldBe(link.TargetUrl);
        (await _links.GetAsync(link.Id)).TargetUrl.ShouldBe(link.TargetUrl);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("default")]
    [InlineData("user")]
    [InlineData("legacy")]
    public async Task Redirect_Should_Read_Staged_Resolution_Cache_Without_Publishing_It(string route)
    {
        var selected = route == "user" ? await CreateNamespaceAsync("CacheCampaign") : null;
        var link = await CreateCacheTestRouteAsync(route, "StagedCacheRead", selected?.Id);
        var key = ShortLinkResolutionCacheKey.Create(Origin, link.Namespace, link.Code);
        const string stagedTarget = "https://example.com/staged-cache";

        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await _cache.SetAsync(key, new ShortLinkResolutionCacheItem
            {
                Exists = true,
                Id = link.Id,
                TenantId = _currentTenant.Id,
                Origin = Origin,
                TargetUrl = stagedTarget,
                Status = ShortLinkStatus.Active
            }, considerUow: true);
            (await ResolveAsync(link)).TargetUrl.ShouldBe(stagedTarget);
            (await _cache.GetAsync(key, considerUow: false)).ShouldBeNull();
            await unit.RollbackAsync();
        }

        (await _cache.GetAsync(key, considerUow: false)).ShouldBeNull();
        (await ResolveAsync(link)).TargetUrl.ShouldBe(link.TargetUrl);
        (await _links.GetAsync(link.Id)).TargetUrl.ShouldBe(link.TargetUrl);
    }

    private Task<ShortLinkDto> CreateCacheTestRouteAsync(string route, string code, Guid? namespaceId)
    {
        if (route != "legacy")
        {
            return CreateLinkAsync(code, namespaceId: namespaceId, useRootPath: route == "root");
        }
        return WithUnitOfWorkAsync(async () =>
        {
            var legacy = new ShortLink(
                Guid.NewGuid(), _currentTenant.Id, _currentUser.GetId(), code, "https://example.com/legacy", null, null);
            await _repository.InsertAsync(legacy, autoSave: true);
            return await _links.GetAsync(legacy.Id);
        });
    }

    private Task<ShortLinkNamespaceDto> CreateNamespaceAsync(string name, string origin = Origin) =>
        _namespaces.CreateAsync(new CreateShortLinkNamespaceDto { Origin = origin, Name = name });

    private Task DeleteNamespaceAsync(ShortLinkNamespaceDto entity) =>
        _namespaces.DeleteAsync(entity.Id, new DeleteShortLinkNamespaceDto { ConcurrencyStamp = entity.ConcurrencyStamp });

    private Task DeleteLinkAsync(bool admin, Guid id, string concurrencyStamp)
    {
        var input = new DeleteShortLinkDto { ConcurrencyStamp = concurrencyStamp };
        return admin ? _admin.DeleteAsync(id, input) : _links.DeleteAsync(id, input);
    }

    private Task<ShortLinkDto> CreateLinkAsync(
        string? code = null,
        string origin = Origin,
        string target = "https://example.com/path",
        Guid? namespaceId = null,
        bool useRootPath = false,
        DateTime? expiresAt = null) =>
        _links.CreateAsync(new CreateShortLinkDto
        {
            Origin = origin,
            TargetUrl = target,
            CustomCode = code ?? $"N{Guid.NewGuid():N}",
            NamespaceId = namespaceId,
            UseRootPath = useRootPath,
            ExpiresAt = expiresAt
        });

    private Task<ShortLinkResolutionDto> ResolveAsync(ShortLinkDto link) =>
        _redirect.ResolveAsync(link.Origin ?? Origin, link.Namespace, link.Code);

    private Task<long> NamespaceCountAsync() =>
        WithUnitOfWorkAsync(() => _namespaceRepository.GetCountByOwnerAsync(_currentUser.GetId()));

    private Task<bool> IsReservedAsync(string name) =>
        WithUnitOfWorkAsync(() => _reservations.IsReservedAsync(name));

    private Task<ShortLinkDomain> CreateDomainAsync(string origin) =>
        WithUnitOfWorkAsync(async () =>
        {
            var domain = await GetRequiredService<ShortLinkDomainManager>().CreateAsync(origin);
            await GetRequiredService<IShortLinkDomainRepository>().InsertAsync(domain, autoSave: true);
            return domain;
        });

    private IDisposable ChangeUser(Guid owner) =>
        GetRequiredService<ICurrentPrincipalAccessor>().Change(CreatePrincipal(owner));

    private static ClaimsPrincipal CreatePrincipal(Guid owner) =>
        new(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, owner.ToString())], "Test"));

    private async Task AssertRouteCacheRemovedAsync(ShortLinkDto mutated, ShortLinkDto control)
    {
        (await _cache.GetAsync(ShortLinkResolutionCacheKey.Create(
            mutated.Origin ?? Origin, mutated.Namespace, mutated.Code))).ShouldBeNull();
        var preserved = await _cache.GetAsync(ShortLinkResolutionCacheKey.Create(
            control.Origin!, control.Namespace, control.Code));
        preserved.ShouldNotBeNull();
        preserved.Id.ShouldBe(control.Id);
    }

    private static string CreateQrSvg(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qr = new SvgQRCode(data);
        return qr.GetGraphic(5);
    }

    // The shared SQLite fixture owns ShortLink tables, not the separate setting-management module.
    // Model that external store's transaction contract while exercising real reservations and ABP settings.
    private sealed class UnitOfWorkSettingStore(IUnitOfWorkManager units) : ISettingManagementStore
    {
        private const string ChangesKey = "Test:NamespaceSettingChanges";
        private readonly ConcurrentDictionary<SettingKey, string> _values = new();
        public string? FailOnSetting { get; set; }

        public string? CommittedValue(string name) =>
            _values.GetValueOrDefault(new SettingKey(name, GlobalSettingValueProvider.ProviderName, null));

        public Task<string> GetOrNullAsync(string name, string providerName, string providerKey)
        {
            var key = new SettingKey(name, providerName, providerKey);
            var changes = GetChanges();
            var value = changes is not null && changes.TryGetValue(key, out var staged)
                ? staged
                : _values.GetValueOrDefault(key);
            return Task.FromResult(value!);
        }

        public Task<List<SettingValue>> GetListAsync(string providerName, string providerKey) =>
            Task.FromResult(GetMergedValues()
                .Where(pair => pair.Key.Provider == providerName && pair.Key.Key == providerKey)
                .Select(pair => new SettingValue(pair.Key.Name, pair.Value)).ToList());

        public async Task<List<SettingValue>> GetListAsync(string[] names, string providerName, string providerKey) =>
            (await GetListAsync(providerName, providerKey)).Where(value => names.Contains(value.Name)).ToList();

        public Task SetAsync(string name, string value, string providerName, string providerKey)
        {
            if (name == FailOnSetting)
            {
                throw new AbpException("Test setting-store write failure.");
            }
            GetChanges(create: true)![new SettingKey(name, providerName, providerKey)] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string name, string providerName, string providerKey)
        {
            GetChanges(create: true)![new SettingKey(name, providerName, providerKey)] = null;
            return Task.CompletedTask;
        }

        private Dictionary<SettingKey, string?>? GetChanges(bool create = false)
        {
            var unit = units.Current;
            if (unit?.Items.TryGetValue(ChangesKey, out var existing) == true)
            {
                return (Dictionary<SettingKey, string?>)existing;
            }
            if (!create)
            {
                return null;
            }
            if (unit is null || !unit.Options.IsTransactional)
            {
                throw new AbpException("Setting writes require the proxied transactional boundary.");
            }
            var changes = new Dictionary<SettingKey, string?>();
            unit.Items[ChangesKey] = changes;
            unit.OnCompleted(() =>
            {
                foreach (var (key, value) in changes)
                {
                    if (value is null)
                    {
                        _values.TryRemove(key, out _);
                    }
                    else
                    {
                        _values[key] = value;
                    }
                }
                return Task.CompletedTask;
            });
            return changes;
        }

        private Dictionary<SettingKey, string> GetMergedValues()
        {
            var values = _values.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var (key, value) in GetChanges() ?? [])
            {
                if (value is null)
                {
                    values.Remove(key);
                }
                else
                {
                    values[key] = value;
                }
            }
            return values;
        }

        private readonly record struct SettingKey(string Name, string Provider, string? Key);
    }
}
