using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.ShortLink.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Settings;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkCapabilityTests
{
    [Fact]
    public void Quota_Should_Distinguish_Denied_Zero_And_Unlimited()
    {
        ShortLinkQuota.Denied.IsGranted.ShouldBeFalse();
        ShortLinkQuota.Denied.IsUnlimited.ShouldBeFalse();
        ShortLinkQuota.Denied.Limit.ShouldBeNull();
        ShortLinkQuota.Denied.AllowsCreation(0).ShouldBeFalse();
        ShortLinkQuota.Limited(0).IsGranted.ShouldBeTrue();
        ShortLinkQuota.Limited(0).AllowsCreation(0).ShouldBeFalse();
        ShortLinkQuota.Unlimited.IsGranted.ShouldBeTrue();
        ShortLinkQuota.Unlimited.IsUnlimited.ShouldBeTrue();
        ShortLinkQuota.Unlimited.AllowsCreation(long.MaxValue).ShouldBeTrue();
        ShortLinkQuota.Limited(long.MaxValue).AllowsCreation(long.MaxValue - 1).ShouldBeTrue();
        ShortLinkQuota.Limited(long.MaxValue).AllowsCreation(long.MaxValue).ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => ShortLinkQuota.Limited(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => ShortLinkQuota.Unlimited.AllowsCreation(-1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("20")]
    public async Task Standalone_Should_Use_Settings_And_Enable_Statistics(string? value)
    {
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(ShortLinkSettings.MaxLinksPerUser).Returns(value);
        var provider = new SettingShortLinkCapabilityProvider(settings);
        var quota = await provider.GetQuotaAsync(null, Guid.NewGuid());

        provider.IsQuotaExternallyManaged.ShouldBeFalse();
        quota.Limit.ShouldBe(value is null ? ShortLinkDefaults.MaxLinksPerUser : 20);
        (await provider.IsStatisticsEnabledAsync(null, Guid.NewGuid())).ShouldBeTrue();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("unlimited")]
    [InlineData("2147483648")]
    public async Task Invalid_Standalone_Settings_Should_Fail_Explicitly(string value)
    {
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(ShortLinkSettings.MaxLinksPerUser).Returns(value);
        var provider = new SettingShortLinkCapabilityProvider(settings);

        await Should.ThrowAsync<AbpException>(() => provider.GetQuotaAsync(null, Guid.NewGuid()));
    }

    [Fact]
    public async Task Cancelled_Capability_Query_Should_Not_Return_A_Grant()
    {
        var provider = new SettingShortLinkCapabilityProvider(Substitute.For<ISettingProvider>());
        await Should.ThrowAsync<OperationCanceledException>(() =>
            provider.GetQuotaAsync(null, Guid.NewGuid(), new CancellationToken(true)));
    }
}
