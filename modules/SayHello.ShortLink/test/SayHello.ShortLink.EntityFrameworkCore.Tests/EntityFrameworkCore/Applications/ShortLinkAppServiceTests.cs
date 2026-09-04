using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.Common.BlockedDomains;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.Public.ShortLinks;
using Shouldly;
using Volo.Abp.Guids;
using Volo.Abp.Users;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkAppServiceTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IShortLinkAppService _appService;
    private readonly IShortLinkRedirectAppService _redirectAppService;
    private readonly IShortLinkRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IBlockedDomainRepository _blockedDomainRepository;
    private readonly IBlockedDomainCache _blockedDomainCache;
    private readonly IGuidGenerator _guidGenerator;

    public ShortLinkAppServiceTests()
    {
        _appService = GetRequiredService<IShortLinkAppService>();
        _redirectAppService = GetRequiredService<IShortLinkRedirectAppService>();
        _repository = GetRequiredService<IShortLinkRepository>();
        _currentUser = GetRequiredService<ICurrentUser>();
        _blockedDomainRepository = GetRequiredService<IBlockedDomainRepository>();
        _blockedDomainCache = GetRequiredService<IBlockedDomainCache>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task User_Should_Create_Resolve_Disable_And_Delete_A_Link()
    {
        var created = await _appService.CreateAsync(
            new CreateShortLinkDto
            {
                TargetUrl = "https://example.com/path",
                CustomCode = "Test123",
                Title = "Test"
            });

        created.OwnerUserId.ShouldBe(_currentUser.Id!.Value);
        created.ShortUrl.ShouldBe("https://go.example.test/Test123");

        var found = await _redirectAppService.ResolveAsync(
            created.Code,
            new RecordShortLinkVisitDto
            {
                IpAddress = "203.0.113.10",
                Referrer = "https://referrer.example/article",
                UserAgent = "Mozilla/5.0 Chrome/120.0 Mobile"
            });
        found.Status.ShouldBe(ShortLinkResolutionStatus.Found);
        found.TargetUrl.ShouldBe("https://example.com/path");

        var current = await _appService.GetAsync(created.Id);
        current.TotalVisitCount.ShouldBe(1);

        var statistics = await _appService.GetStatisticsAsync(created.Id, 1);
        statistics.TotalVisitCount.ShouldBe(1);
        statistics.UniqueVisitorCount.ShouldBe(1);
        statistics.Daily.Single().VisitCount.ShouldBe(1);
        statistics.Referrers.Single().Value.ShouldBe("referrer.example");
        statistics.Browsers.Single().Value.ShouldBe("Chrome");
        statistics.Devices.Single().Value.ShouldBe("Mobile");

        var disabled = await _appService.SetStatusAsync(
            created.Id,
            new SetShortLinkStatusDto
            {
                Status = ShortLinkStatus.Disabled,
                ConcurrencyStamp = current.ConcurrencyStamp
            });
        disabled.Status.ShouldBe(ShortLinkStatus.Disabled);

        (await _redirectAppService.ResolveAsync(created.Code)).Status
            .ShouldBe(ShortLinkResolutionStatus.Gone);

        await _appService.DeleteAsync(created.Id);
        (await _repository.CodeExistsAsync(created.Code)).ShouldBeTrue();
        (await _redirectAppService.ResolveAsync(created.Code)).Status
            .ShouldBe(ShortLinkResolutionStatus.Gone);
    }

    [Fact]
    public async Task Resolve_Should_Block_Target_Without_Recording_A_Visit()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var targetHost = $"{suffix}.example";
        var created = await _appService.CreateAsync(
            new CreateShortLinkDto
            {
                TargetUrl = $"https://{targetHost}/path",
                CustomCode = $"B{suffix[..6]}"
            });

        await WithUnitOfWorkAsync(() =>
            _blockedDomainRepository.InsertAsync(
                new BlockedDomain(
                    _guidGenerator.Create(),
                    null,
                    targetHost,
                    "Unsafe destination"),
                autoSave: true));
        await _blockedDomainCache.InvalidateAsync(targetHost, null);

        var result = await _redirectAppService.ResolveAsync(
            created.Code,
            new RecordShortLinkVisitDto
            {
                IpAddress = "203.0.113.10",
                UserAgent = "Mozilla/5.0 Chrome/120.0"
            });

        result.Status.ShouldBe(ShortLinkResolutionStatus.Blocked);
        result.BlockedDomain.ShouldBe(targetHost);
        result.BlockedReason.ShouldBe("Unsafe destination");
        (await _appService.GetAsync(created.Id)).TotalVisitCount.ShouldBe(0);
    }
}
