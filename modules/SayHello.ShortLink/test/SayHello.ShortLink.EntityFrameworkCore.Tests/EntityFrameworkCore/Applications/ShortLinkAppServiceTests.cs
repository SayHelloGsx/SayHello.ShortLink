using System;
using System.Linq;
using System.Threading.Tasks;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.Public.ShortLinks;
using Shouldly;
using Volo.Abp.Users;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkAppServiceTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IShortLinkAppService _appService;
    private readonly IShortLinkRedirectAppService _redirectAppService;
    private readonly IShortLinkRepository _repository;
    private readonly ICurrentUser _currentUser;

    public ShortLinkAppServiceTests()
    {
        _appService = GetRequiredService<IShortLinkAppService>();
        _redirectAppService = GetRequiredService<IShortLinkRedirectAppService>();
        _repository = GetRequiredService<IShortLinkRepository>();
        _currentUser = GetRequiredService<ICurrentUser>();
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
}
