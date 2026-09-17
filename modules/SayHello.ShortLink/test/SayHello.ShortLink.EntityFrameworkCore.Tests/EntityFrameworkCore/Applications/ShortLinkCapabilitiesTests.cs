using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SayHello.ShortLink.Admin.Settings;
using SayHello.ShortLink.Admin.ShortLinks;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinkDomains;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Json;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkCapabilitiesTests :
    ShortLinkTestBase<ShortLinkCapabilitiesTestModule>,
    IAsyncLifetime
{
    private IShortLinkCapabilityProvider _capabilities = null!;
    private IPermissionChecker _permissions = null!;
    private StatisticsReads _statisticsReads = null!;
    private bool _seedCreatedCounters;
    private bool _statisticsPermissionGranted = true;
    private readonly IShortLinkAppService _appService;
    private readonly IShortLinkRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IJsonSerializer _serializer;

    public ShortLinkCapabilitiesTests()
    {
        _appService = GetRequiredService<IShortLinkAppService>();
        _repository = GetRequiredService<IShortLinkRepository>();
        _currentUser = GetRequiredService<ICurrentUser>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _serializer = GetRequiredService<IJsonSerializer>();
    }

    public async Task InitializeAsync()
    {
        await CreateDomainAsync("https://go.example.test");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected override void AfterAddApplication(IServiceCollection services)
    {
        base.AfterAddApplication(services);
        _capabilities = Substitute.For<IShortLinkCapabilityProvider>();
        _capabilities.GetQuotaAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ShortLinkQuota.Unlimited);
        _capabilities.GetDomainAccessAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ShortLinkDomainAccess.Unrestricted);
        _permissions = Substitute.For<IPermissionChecker>();
        _permissions.IsGrantedAsync(Arg.Any<string>())
            .Returns(call =>
                call.Arg<string>() != ShortLinkPublicPermissions.ViewStatistics ||
                _statisticsPermissionGranted);
        _permissions.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>())
            .Returns(call =>
                call.ArgAt<string>(1) != ShortLinkPublicPermissions.ViewStatistics ||
                _statisticsPermissionGranted);
        SetStatisticsEnabled(true);
        services.AddSingleton(_capabilities);
        services.AddSingleton(_permissions);
        _statisticsReads = new StatisticsReads();
        services.AddTransient<IShortLinkStatisticsRepository>(provider =>
            new TrackingStatisticsRepository(
                new EfCoreShortLinkStatisticsRepository(
                    provider.GetRequiredService<IDbContextProvider<IShortLinkDbContext>>()),
                _statisticsReads));
        services.AddTransient<IShortLinkCacheInvalidator>(provider =>
            new CounterSeedingCacheInvalidator(
                provider.GetRequiredService<ShortLinkCacheInvalidator>(),
                provider.GetRequiredService<IShortLinkRepository>(),
                () => _seedCreatedCounters));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Every_Public_Response_Should_Respect_Statistics_Without_Changing_Stored_Counters(bool enabled)
    {
        SetStatisticsEnabled(enabled);
        _seedCreatedCounters = true;

        var created = await CreateAsync();
        AssertSerializedCounter(created, enabled ? 2 : null);
        (await WithUnitOfWorkAsync(() => _repository.GetAsync(created.Id))).TotalVisitCount.ShouldBe(2);

        var found = await _appService.GetAsync(created.Id);
        AssertSerializedCounter(found, enabled ? 2 : null);

        var updated = await _appService.UpdateAsync(created.Id, new UpdateShortLinkDto
        {
            TargetUrl = "https://example.com/updated",
            Title = "Changed",
            ConcurrencyStamp = found.ConcurrencyStamp
        });
        AssertSerializedCounter(updated, enabled ? 2 : null);

        var disabled = await _appService.SetStatusAsync(created.Id, new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Disabled,
            ConcurrencyStamp = updated.ConcurrencyStamp
        });
        AssertSerializedCounter(disabled, enabled ? 2 : null);
        await CreateAsync();

        _capabilities.ClearReceivedCalls();
        var list = await _appService.GetListAsync(new GetShortLinksInput());
        list.TotalCount.ShouldBe(2);
        foreach (var item in list.Items)
        {
            AssertSerializedCounter(item, enabled ? 2 : null);
        }

        using var document = JsonDocument.Parse(_serializer.Serialize(list));
        foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
        {
            AssertJsonCounter(item.GetProperty("totalVisitCount"), enabled ? 2 : null);
        }

        await _capabilities.Received(1).GetStatisticsLevelAsync(
            _currentUser.GetId(), Arg.Any<CancellationToken>());
        (await WithUnitOfWorkAsync(() => _repository.GetAsync(created.Id))).TotalVisitCount.ShouldBe(2);
    }

    [Theory]
    [InlineData("totalvisitcount")]
    [InlineData("totalvisitcount desc")]
    [InlineData("totalvisitcount asc")]
    [InlineData("  TOTALVISITCOUNT DESC  ")]
    [InlineData("\tTotalVisitCount\tAsc \r\n")]
    public async Task Disabled_Statistics_Should_Reject_Visit_Sorting(string sorting)
    {
        SetStatisticsEnabled(false);
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _appService.GetListAsync(new GetShortLinksInput { Sorting = sorting }));
        exception.Code.ShouldBe(ShortLinkErrorCodes.StatisticsNotGranted);
        _statisticsReads.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Disabled_Statistics_Should_Deny_Details_Before_Reading_Statistics_And_Keep_Ownership()
    {
        var created = await CreateAsync();
        SetStatisticsEnabled(false);
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _appService.GetStatisticsAsync(created.Id));
        exception.Code.ShouldBe(ShortLinkErrorCodes.StatisticsNotGranted);
        _statisticsReads.Count.ShouldBe(0);

        _capabilities.ClearReceivedCalls();
        using (ChangeUser(Guid.NewGuid()))
        {
            exception = await Should.ThrowAsync<BusinessException>(() =>
                _appService.GetStatisticsAsync(created.Id));
            exception.Code.ShouldBe(ShortLinkErrorCodes.LinkAccessDenied);
            await _capabilities.DidNotReceive().GetStatisticsLevelAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _appService.GetStatisticsAsync(created.Id));
        }

        _statisticsReads.Count.ShouldBe(0);
        (await _appService.GetQrCodeAsync(created.Id)).Content.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Statistics_Collection_And_Admin_Counters_Should_Continue_When_Public_Statistics_Are_Disabled()
    {
        var first = await CreateAsync();
        var second = await CreateAsync();
        SetStatisticsEnabled(false);
        var redirect = GetRequiredService<IShortLinkRedirectAppService>();
        foreach (var code in new[] { first.Code, first.Code, second.Code })
        {
            (await redirect.ResolveAsync(first.Origin!, code, new RecordShortLinkVisitDto
            {
                IpAddress = "203.0.113.10",
                UserAgent = "Mozilla/5.0 Chrome/120.0"
            })).Status.ShouldBe(ShortLinkResolutionStatus.Found);
        }

        AssertSerializedCounter(await _appService.GetAsync(first.Id), null);
        var admin = GetRequiredService<IShortLinkAdministrationAppService>();
        var adminList = await admin.GetListAsync(new GetShortLinksInput { Sorting = "totalvisitcount desc" });
        adminList.Items.First().Id.ShouldBe(first.Id);
        AssertSerializedCounter(adminList.Items.First(), 2);
        AssertSerializedCounter(adminList.Items.Last(), 1);
        var updated = await admin.SetStatusAsync(first.Id, new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Disabled,
            ConcurrencyStamp = adminList.Items.First().ConcurrencyStamp
        });
        AssertSerializedCounter(updated, 2);

        SetStatisticsEnabled(true);
        var statistics = await _appService.GetStatisticsAsync(first.Id);
        statistics.StatisticsLevel.ShouldBe(ShortLinkStatisticsLevel.Advanced);
        statistics.TotalVisitCount.ShouldBe(2);
        statistics.UniqueVisitorCount.ShouldBe(1);
        statistics.Daily.Sum(x => x.VisitCount).ShouldBe(2);
        _statisticsReads.Count.ShouldBe(1);
        var publicList = await _appService.GetListAsync(new GetShortLinksInput { Sorting = "totalvisitcount desc" });
        publicList.Items.First().Id.ShouldBe(first.Id);
        AssertSerializedCounter(publicList.Items.First(), 2);
    }

    [Fact]
    public async Task Basic_Statistics_Should_Return_Only_Total_Without_Reading_Advanced_Data()
    {
        var created = await CreateAsync();
        var redirect = GetRequiredService<IShortLinkRedirectAppService>();
        await redirect.ResolveAsync(created.Origin!, created.Code, new RecordShortLinkVisitDto
        {
            IpAddress = "203.0.113.10",
            UserAgent = "Mozilla/5.0 Chrome/120.0"
        });
        SetStatisticsLevel(ShortLinkStatisticsLevel.Basic);

        var statistics = await _appService.GetStatisticsAsync(created.Id);

        statistics.StatisticsLevel.ShouldBe(ShortLinkStatisticsLevel.Basic);
        statistics.TotalVisitCount.ShouldBe(1);
        statistics.UniqueVisitorCount.ShouldBeNull();
        statistics.Daily.ShouldBeEmpty();
        statistics.Referrers.ShouldBeEmpty();
        statistics.Browsers.ShouldBeEmpty();
        statistics.Devices.ShouldBeEmpty();
        _statisticsReads.Count.ShouldBe(0);

        var list = await _appService.GetListAsync(
            new GetShortLinksInput { Sorting = "totalvisitcount desc" });
        AssertSerializedCounter(list.Items.Single(), 1);
    }

    [Fact]
    public async Task Advanced_Statistics_Without_Permission_Should_Mask_Summaries_And_Reject_Sorting()
    {
        SetStatisticsLevel(ShortLinkStatisticsLevel.Advanced);
        _statisticsPermissionGranted = false;
        _seedCreatedCounters = true;

        var created = await CreateAsync();
        AssertSerializedCounter(created, null);
        (await WithUnitOfWorkAsync(() => _repository.GetAsync(created.Id)))
            .TotalVisitCount.ShouldBe(2);

        var found = await _appService.GetAsync(created.Id);
        AssertSerializedCounter(found, null);

        var updated = await _appService.UpdateAsync(created.Id, new UpdateShortLinkDto
        {
            TargetUrl = "https://example.com/updated",
            Title = "Changed",
            ConcurrencyStamp = found.ConcurrencyStamp
        });
        AssertSerializedCounter(updated, null);

        var disabled = await _appService.SetStatusAsync(created.Id, new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Disabled,
            ConcurrencyStamp = updated.ConcurrencyStamp
        });
        AssertSerializedCounter(disabled, null);

        var list = await _appService.GetListAsync(new GetShortLinksInput());
        AssertSerializedCounter(list.Items.Single(), null);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _appService.GetListAsync(
                new GetShortLinksInput { Sorting = "totalvisitcount desc" }));
        exception.Code.ShouldBe(ShortLinkErrorCodes.StatisticsNotGranted);
        _statisticsReads.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Capabilities_Should_Distinguish_Denied_Zero_Finite_Overage_And_Unlimited()
    {
        var empty = await _appService.GetCapabilitiesAsync();
        empty.UsedLinks.ShouldBe(0);
        empty.IsUnlimited.ShouldBeTrue();
        empty.MaxLinks.ShouldBeNull();
        empty.RemainingLinks.ShouldBeNull();

        await CreateAsync();
        await CreateAsync();
        foreach (var limit in new[] { 0L, 1L, 2L, 5L, long.MaxValue })
        {
            SetQuota(ShortLinkQuota.Limited(limit));
            var result = await _appService.GetCapabilitiesAsync();
            result.UsedLinks.ShouldBe(2);
            result.IsQuotaGranted.ShouldBeTrue();
            result.IsUnlimited.ShouldBeFalse();
            result.MaxLinks.ShouldBe(limit);
            result.RemainingLinks.ShouldBe(Math.Max(0, limit - 2));
            result.StatisticsLevel.ShouldBe(ShortLinkStatisticsLevel.Advanced);
            result.StatisticsEnabled.ShouldBeTrue();
        }

        SetQuota(ShortLinkQuota.Denied);
        SetStatisticsEnabled(false);
        var denied = await _appService.GetCapabilitiesAsync();
        denied.UsedLinks.ShouldBe(2);
        denied.IsQuotaGranted.ShouldBeFalse();
        denied.IsUnlimited.ShouldBeFalse();
        denied.MaxLinks.ShouldBeNull();
        denied.RemainingLinks.ShouldBe(0);
        denied.StatisticsLevel.ShouldBe(ShortLinkStatisticsLevel.None);
        denied.StatisticsEnabled.ShouldBeFalse();

        SetQuota(ShortLinkQuota.Unlimited);
        var unlimited = await _appService.GetCapabilitiesAsync();
        unlimited.UsedLinks.ShouldBe(2);
        unlimited.IsQuotaGranted.ShouldBeTrue();
        unlimited.IsUnlimited.ShouldBeTrue();
        unlimited.MaxLinks.ShouldBeNull();
        unlimited.RemainingLinks.ShouldBeNull();
    }

    [Fact]
    public async Task Usage_Should_Count_Disabled_Expired_Links_And_Release_Deleted_Links_For_Current_Owner_Only()
    {
        var first = await CreateAsync();
        await _appService.SetStatusAsync(first.Id, new SetShortLinkStatusDto
        {
            Status = ShortLinkStatus.Disabled,
            ConcurrencyStamp = first.ConcurrencyStamp
        });
        await CreateAsync(DateTime.UtcNow.AddDays(-1));
        var deleted = await CreateAsync();
        await _appService.DeleteAsync(deleted.Id);
        SetQuota(ShortLinkQuota.Limited(5));

        var result = await _appService.GetCapabilitiesAsync();
        result.UsedLinks.ShouldBe(2);
        result.RemainingLinks.ShouldBe(3);

        using (ChangeUser(Guid.NewGuid()))
        {
            (await _appService.GetCapabilitiesAsync()).UsedLinks.ShouldBe(0);
        }

        var otherTenantId = Guid.NewGuid();
        using (_currentTenant.Change(otherTenantId))
        {
            await CreateDomainAsync("https://go.example.test");
            (await _appService.GetCapabilitiesAsync()).UsedLinks.ShouldBe(0);
        }

        await GetRequiredService<IShortLinkAdministrationAppService>().DeleteAsync(first.Id);
        (await _appService.GetCapabilitiesAsync()).RemainingLinks.ShouldBe(4);
    }

    [Fact]
    public async Task Domain_Access_Should_Keep_Default_Open_And_Isolate_Resolution_By_Origin()
    {
        var domainManager = GetRequiredService<ShortLinkDomainManager>();
        var domainRepository = GetRequiredService<IShortLinkDomainRepository>();
        var extra = await CreateDomainAsync("HTTPS://ALT.EXAMPLE.TEST:443/");
        _capabilities.GetDomainAccessAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ShortLinkDomainAccess.Restricted([]));

        var restricted = await _appService.GetCapabilitiesAsync();
        restricted.Domains.ShouldHaveSingleItem().Origin
            .ShouldBe("https://go.example.test");

        const string sharedCode = "Shared01";
        var defaultLink = await _appService.CreateAsync(new CreateShortLinkDto
        {
            Origin = "https://go.example.test",
            TargetUrl = "https://example.com/default",
            CustomCode = sharedCode
        });
        var denied = await Should.ThrowAsync<BusinessException>(() =>
            _appService.CreateAsync(new CreateShortLinkDto
            {
                Origin = extra.Origin,
                TargetUrl = "https://example.org/denied",
                CustomCode = sharedCode
            }));
        denied.Code.ShouldBe(ShortLinkErrorCodes.DomainAccessDenied);

        _capabilities.GetDomainAccessAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ShortLinkDomainAccess.Restricted([extra.Origin]));
        var available = await _appService.GetCapabilitiesAsync();
        available.Domains.Select(x => x.Origin).ShouldBe(
            ["https://go.example.test", extra.Origin],
            ignoreOrder: true);

        var extraLink = await _appService.CreateAsync(new CreateShortLinkDto
        {
            Origin = extra.Origin,
            TargetUrl = "https://example.org/extra",
            CustomCode = sharedCode
        });
        var redirect = GetRequiredService<IShortLinkRedirectAppService>();
        (await redirect.ResolveAsync(defaultLink.Origin!, sharedCode)).TargetUrl
            .ShouldBe("https://example.com/default");
        (await redirect.ResolveAsync(extraLink.Origin!, sharedCode)).TargetUrl
            .ShouldBe("https://example.org/extra");
        (await redirect.ResolveAsync("https://wrong.example.test", sharedCode)).Status
            .ShouldBe(ShortLinkResolutionStatus.NotFound);

        await WithUnitOfWorkAsync(async () =>
        {
            var currentExtra = await domainRepository.GetAsync(extra.Id);
            currentExtra.Disable();
            await domainRepository.UpdateAsync(currentExtra, autoSave: true);
        });
        (await _appService.GetCapabilitiesAsync()).Domains
            .ShouldHaveSingleItem().IsDefault.ShouldBeTrue();
        denied = await Should.ThrowAsync<BusinessException>(() =>
            _appService.CreateAsync(new CreateShortLinkDto
            {
                Origin = extra.Origin,
                TargetUrl = "https://example.org/new",
                CustomCode = "Extra02"
            }));
        denied.Code.ShouldBe(ShortLinkErrorCodes.DomainAccessDenied);
        (await redirect.ResolveAsync(extraLink.Origin!, sharedCode)).Status
            .ShouldBe(ShortLinkResolutionStatus.Found);

        await _appService.DeleteAsync(extraLink.Id);
        var currentExtra = await WithUnitOfWorkAsync(() =>
            domainRepository.GetAsync(extra.Id));
        var inUse = await Should.ThrowAsync<BusinessException>(() =>
            domainManager.EnsureCanDeleteAsync(currentExtra));
        inUse.Code.ShouldBe(ShortLinkErrorCodes.DomainInUse);
        (await WithUnitOfWorkAsync(() => domainRepository.GetAsync(extra.Id)))
            .Id.ShouldBe(extra.Id);
    }

    private Task<ShortLinkDomain> CreateDomainAsync(string origin)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            var domain = await GetRequiredService<ShortLinkDomainManager>()
                .CreateAsync(origin);
            await GetRequiredService<IShortLinkDomainRepository>()
                .InsertAsync(domain, autoSave: true);
            return domain;
        });
    }

    [Fact]
    public async Task Capabilities_Should_Require_An_Authenticated_Current_Owner()
    {
        using var anonymous = GetRequiredService<ICurrentPrincipalAccessor>()
            .Change(new ClaimsPrincipal(new ClaimsIdentity()));
        _capabilities.ClearReceivedCalls();
        await Should.ThrowAsync<InvalidOperationException>(() => _appService.GetCapabilitiesAsync());
        await _capabilities.DidNotReceive().GetQuotaAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Settings_Should_Report_External_Quota_Management_Without_Changing_Standalone_Limit()
    {
        var settingsService = GetRequiredService<IShortLinkSettingsAppService>();
        var standalone = await settingsService.GetAsync();
        standalone.IsQuotaExternallyManaged.ShouldBeFalse();

        _capabilities.IsQuotaExternallyManaged.Returns(true);
        var external = await settingsService.GetAsync();
        external.IsQuotaExternallyManaged.ShouldBeTrue();
        external.MaxLinksPerUser.ShouldBe(standalone.MaxLinksPerUser);

        external.IsQuotaExternallyManaged = false;
        var saved = await settingsService.UpdateAsync(external);
        saved.IsQuotaExternallyManaged.ShouldBeTrue();
        saved.MaxLinksPerUser.ShouldBe(standalone.MaxLinksPerUser);
    }

    private Task<ShortLinkDto> CreateAsync(DateTime? expiresAt = null)
    {
        return _appService.CreateAsync(new CreateShortLinkDto
        {
            Origin = "https://go.example.test",
            TargetUrl = "https://example.com/path",
            CustomCode = $"C{Guid.NewGuid():N}",
            ExpiresAt = expiresAt
        });
    }

    private void SetStatisticsEnabled(bool enabled)
    {
        SetStatisticsLevel(enabled
            ? ShortLinkStatisticsLevel.Advanced
            : ShortLinkStatisticsLevel.None);
    }

    private void SetStatisticsLevel(ShortLinkStatisticsLevel level)
    {
        _capabilities.GetStatisticsLevelAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(level);
    }

    private void SetQuota(ShortLinkQuota quota)
    {
        _capabilities.GetQuotaAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(quota);
    }

    private IDisposable ChangeUser(Guid userId)
    {
        return GetRequiredService<ICurrentPrincipalAccessor>().Change(
            new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) }, "Test")));
    }

    private void AssertSerializedCounter(ShortLinkDto dto, long? expected)
    {
        dto.TotalVisitCount.ShouldBe(expected);
        using var document = JsonDocument.Parse(_serializer.Serialize(dto));
        AssertJsonCounter(document.RootElement.GetProperty("totalVisitCount"), expected);
    }

    private static void AssertJsonCounter(JsonElement counter, long? expected)
    {
        if (expected.HasValue)
        {
            counter.GetInt64().ShouldBe(expected.Value);
        }
        else
        {
            counter.ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    private sealed class StatisticsReads
    {
        public int Count { get; set; }
    }

    private sealed class CounterSeedingCacheInvalidator(
        IShortLinkCacheInvalidator inner,
        IShortLinkRepository repository,
        Func<bool> shouldSeed) : IShortLinkCacheInvalidator
    {
        public async Task RemoveAsync(
            string? origin,
            string code,
            CancellationToken cancellationToken = default)
        {
            if (shouldSeed())
            {
                // Persist a nonzero counter after insertion but before the create response is mapped.
                var entity = await repository.FindByCodeAsync(
                    origin!,
                    code,
                    cancellationToken: cancellationToken);
                if (entity is { TotalVisitCount: 0 })
                {
                    entity.IncreaseVisitCount();
                    entity.IncreaseVisitCount();
                    await repository.UpdateAsync(entity, autoSave: true, cancellationToken: cancellationToken);
                }
            }

            await inner.RemoveAsync(origin, code, cancellationToken);
        }
    }

    private sealed class TrackingStatisticsRepository(
        IShortLinkStatisticsRepository inner,
        StatisticsReads reads) : IShortLinkStatisticsRepository
    {
        public Task<ShortLinkStatisticsData> GetAsync(
            Guid shortLinkId,
            DateOnly startDate,
            DateOnly endDate,
            int maxDimensionItems,
            CancellationToken cancellationToken = default)
        {
            reads.Count++;
            return inner.GetAsync(shortLinkId, startDate, endDate, maxDimensionItems, cancellationToken);
        }
    }
}
