using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using SayHello.ShortLink.BlockedDomains;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class TargetUrlValidatorTests
{
    private readonly IBlockedDomainRepository _blockedDomains =
        Substitute.For<IBlockedDomainRepository>();

    private readonly IHostAddressResolver _resolver =
        Substitute.For<IHostAddressResolver>();

    [Fact]
    public async Task ValidateAsync_Should_Normalize_Public_Http_Url()
    {
        _resolver
            .ResolveAsync("example.com", Arg.Any<CancellationToken>())
            .Returns(new List<IPAddress> { IPAddress.Parse("93.184.216.34") });
        var validator = CreateValidator();

        var result = await validator.ValidateAsync("HTTPS://Example.COM:443/a?b=1", null);

        result.NormalizedHost.ShouldBe("example.com");
        result.NormalizedUrl.ShouldBe("https://example.com/a?b=1");
    }

    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://169.254.1.1")]
    [InlineData("http://[::1]")]
    [InlineData("http://[fc00::1]")]
    public async Task ValidateAsync_Should_Reject_NonPublic_Literal_Address(string target)
    {
        var validator = CreateValidator();

        var exception = await Should.ThrowAsync<BusinessException>(
            () => validator.ValidateAsync(target, null));

        exception.Code.ShouldBe(ShortLinkErrorCodes.UnsafeTargetUrl);
    }

    [Fact]
    public async Task ValidateAsync_Should_Reject_Host_When_Dns_Returns_Private_Address()
    {
        _resolver
            .ResolveAsync("example.com", Arg.Any<CancellationToken>())
            .Returns(new List<IPAddress> { IPAddress.Parse("192.168.1.10") });
        var validator = CreateValidator();

        var exception = await Should.ThrowAsync<BusinessException>(
            () => validator.ValidateAsync("https://example.com", null));

        exception.Code.ShouldBe(ShortLinkErrorCodes.UnsafeTargetUrl);
    }

    [Fact]
    public async Task ValidateAsync_Should_Reject_Blocked_Domain_And_Subdomains()
    {
        _blockedDomains
            .IsBlockedAsync("sub.example.com", null, Arg.Any<CancellationToken>())
            .Returns(true);
        var validator = CreateValidator();

        var exception = await Should.ThrowAsync<BusinessException>(
            () => validator.ValidateAsync("https://sub.example.com", null));

        exception.Code.ShouldBe(ShortLinkErrorCodes.BlockedTargetDomain);
    }

    [Fact]
    public async Task ValidateAsync_Should_Reject_Own_Host()
    {
        var options = new ShortLinkSecurityOptions();
        options.OwnHosts.Add("go.example.com");
        var validator = CreateValidator(options);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => validator.ValidateAsync("https://go.example.com/another-code", null));

        exception.Code.ShouldBe(ShortLinkErrorCodes.UnsafeTargetUrl);
    }

    private TargetUrlValidator CreateValidator(ShortLinkSecurityOptions? options = null)
    {
        return new TargetUrlValidator(
            _blockedDomains,
            _resolver,
            Options.Create(options ?? new ShortLinkSecurityOptions()));
    }
}
