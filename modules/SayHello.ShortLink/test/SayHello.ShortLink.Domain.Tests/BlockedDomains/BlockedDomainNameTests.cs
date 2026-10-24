using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.ShortLink.BlockedDomains;

public class BlockedDomainNameTests
{
    [Theory]
    [InlineData(" Example.COM. ", "example.com")]
    [InlineData("localhost", "localhost")]
    [InlineData("Bücher.Example", "xn--bcher-kva.example")]
    [InlineData("3405803781", "203.0.113.5")]
    [InlineData("5", "0.0.0.5")]
    [InlineData("2001:DB8::1", "2001:db8::1")]
    [InlineData("::FFFF:CB00:7105", "::ffff:203.0.113.5")]
    public void TryNormalize_Should_Return_Dns_Names_And_Canonical_Ip_Addresses(
        string domain,
        string expected)
    {
        BlockedDomainName.TryNormalize(domain, out var normalizedDomain).ShouldBeTrue();

        normalizedDomain.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("example.com:443")]
    [InlineData("fe80::1%eth0")]
    [InlineData("[fe80::1%3]")]
    public void TryNormalize_Should_Reject_Values_That_Are_Not_A_Host(string? domain)
    {
        BlockedDomainName.TryNormalize(domain, out var normalizedDomain).ShouldBeFalse();

        normalizedDomain.ShouldBeNull();
    }

    [Fact]
    public void Normalize_Should_Throw_A_Localized_Business_Exception()
    {
        var exception = Should.Throw<BusinessException>(() =>
            BlockedDomainName.Normalize(" example.com/path "));

        exception.Code.ShouldBe(ShortLinkErrorCodes.InvalidBlockedDomain);
        exception.Data["Domain"].ShouldBe("example.com/path");
    }
}
