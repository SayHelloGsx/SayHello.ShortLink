using System;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using Xunit;

namespace SayHello.ShortLink.Public.ShortLinks;

public class InMemoryShortLinkCreationRateLimiterTests
{
    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private DateTime _now = new(2026, 9, 4, 8, 0, 0, DateTimeKind.Utc);

    public InMemoryShortLinkCreationRateLimiterTests()
    {
        _clock.Now.Returns(_ => _now);
        _settingProvider.GetOrNullAsync(ShortLinkSettings.CreateLimitPerHour).Returns("2");
    }

    [Fact]
    public async Task EnsureAllowedAsync_Should_Reject_Creations_Over_The_Limit_Until_The_Window_Expires()
    {
        var limiter = CreateLimiter();
        var userId = Guid.NewGuid();

        await limiter.EnsureAllowedAsync(userId);
        await limiter.EnsureAllowedAsync(userId);
        var exception = await Should.ThrowAsync<BusinessException>(
            () => limiter.EnsureAllowedAsync(userId));
        exception.Code.ShouldBe(ShortLinkErrorCodes.CreationRateExceeded);
        exception.Data["Limit"].ShouldBe(2);

        _now = _now.AddMinutes(59);
        await Should.ThrowAsync<BusinessException>(() => limiter.EnsureAllowedAsync(userId));

        _now = _now.AddMinutes(1);
        await limiter.EnsureAllowedAsync(userId);
    }

    [Fact]
    public async Task EnsureAllowedAsync_Should_Count_Each_Tenant_And_User_Separately()
    {
        _settingProvider.GetOrNullAsync(ShortLinkSettings.CreateLimitPerHour).Returns("1");
        var limiter = CreateLimiter();
        var userId = Guid.NewGuid();

        await limiter.EnsureAllowedAsync(userId);
        await Should.ThrowAsync<BusinessException>(() => limiter.EnsureAllowedAsync(userId));

        await limiter.EnsureAllowedAsync(Guid.NewGuid());
        _currentTenant.Id.Returns(Guid.NewGuid());
        await limiter.EnsureAllowedAsync(userId);
    }

    [Fact]
    public async Task EnsureAllowedAsync_Should_Evict_Expired_Windows_Once_Per_Window_Length()
    {
        var limiter = CreateLimiter();

        await limiter.EnsureAllowedAsync(Guid.NewGuid());
        await limiter.EnsureAllowedAsync(Guid.NewGuid());
        _now = _now.AddMinutes(30);
        await limiter.EnsureAllowedAsync(Guid.NewGuid());
        limiter.TrackedWindowCount.ShouldBe(3);

        _now = _now.AddMinutes(30);
        await limiter.EnsureAllowedAsync(Guid.NewGuid());
        limiter.TrackedWindowCount.ShouldBe(2);

        _now = _now.AddMinutes(45);
        await limiter.EnsureAllowedAsync(Guid.NewGuid());
        limiter.TrackedWindowCount.ShouldBe(3);

        _now = _now.AddMinutes(15);
        await limiter.EnsureAllowedAsync(Guid.NewGuid());
        limiter.TrackedWindowCount.ShouldBe(2);
    }

    private InMemoryShortLinkCreationRateLimiter CreateLimiter()
    {
        return new InMemoryShortLinkCreationRateLimiter(_settingProvider, _clock, _currentTenant);
    }
}
