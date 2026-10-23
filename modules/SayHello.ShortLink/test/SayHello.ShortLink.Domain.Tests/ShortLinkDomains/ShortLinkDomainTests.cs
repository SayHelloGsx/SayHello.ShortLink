using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.ShortLink.ShortLinkNamespaces;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainTests
{
    [Theory]
    [InlineData("HTTPS://BÜCHER.Example:443/", "https://xn--bcher-kva.example")]
    [InlineData("http://Example.COM:80", "http://example.com")]
    [InlineData("https://Example.COM:8443/", "https://example.com:8443")]
    [InlineData("https://Example.COM./", "https://example.com")]
    [InlineData("http://[2001:db8::1]:80/", "http://[2001:db8::1]")]
    public void Origin_Should_Be_Normalized(string input, string expected)
    {
        ShortLinkDomainOrigin.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("https://user@example.com")]
    [InlineData("https://@example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/path/..")]
    [InlineData("https://example.com/./")]
    [InlineData("https://example.com?")]
    [InlineData("https://example.com?query=1")]
    [InlineData("https://example.com#fragment")]
    [InlineData("https://example.com//")]
    public void Origin_Should_Reject_Non_Origin_Values(string input)
    {
        var exception = Should.Throw<BusinessException>(
            () => ShortLinkDomainOrigin.Normalize(input));

        exception.Code.ShouldBe(ShortLinkErrorCodes.InvalidDomainOrigin);
    }

    [Fact]
    public void Default_Domain_Should_Stay_Enabled_And_Cannot_Be_Deleted()
    {
        var domain = new ShortLinkDomain(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://GO.example.com/",
            isDefault: true);

        domain.Origin.ShouldBe("https://go.example.com");
        domain.IsDefault.ShouldBeTrue();
        domain.IsEnabled.ShouldBeTrue();
        Should.Throw<BusinessException>(domain.Disable).Code
            .ShouldBe(ShortLinkErrorCodes.DefaultDomainCannotBeDisabled);
        Should.Throw<BusinessException>(domain.EnsureCanDelete).Code
            .ShouldBe(ShortLinkErrorCodes.DefaultDomainCannotBeDeleted);
    }

    [Fact]
    public void Ordinary_Domain_Should_Allow_Enable_And_Disable()
    {
        var domain = new ShortLinkDomain(
            Guid.NewGuid(),
            null,
            "https://go.example.com");

        domain.Disable();
        domain.IsEnabled.ShouldBeFalse();
        domain.Enable();
        domain.IsEnabled.ShouldBeTrue();
        Should.NotThrow(domain.EnsureCanDelete);
    }

    [Fact]
    public async Task Delete_Policy_Should_Count_All_References()
    {
        var repository = Substitute.For<IShortLinkDomainRepository>();
        var domain = new ShortLinkDomain(
            Guid.NewGuid(),
            null,
            "https://go.example.com");
        repository
            .GetShortLinkReferenceCountAsync(
                domain.Id,
                Arg.Any<CancellationToken>())
            .Returns(3);
        var policy = new ShortLinkDomainDeletionPolicy(
            repository, Substitute.For<IShortLinkNamespaceRepository>());

        var exception = await Should.ThrowAsync<BusinessException>(
            () => policy.EnsureCanDeleteAsync(domain));

        exception.Code.ShouldBe(ShortLinkErrorCodes.DomainInUse);
        exception.Data["Origin"].ShouldBe(domain.Origin);
        exception.Data["ReferenceCount"].ShouldBe(3L);
    }

    [Fact]
    public async Task A_Namespace_Should_Prevent_Deleting_A_Domain_Without_Links()
    {
        var links = Substitute.For<IShortLinkDomainRepository>();
        var namespaces = Substitute.For<IShortLinkNamespaceRepository>();
        var domain = new ShortLinkDomain(Guid.NewGuid(), null, "https://go.example.com");
        using var cancellation = new CancellationTokenSource();
        namespaces.HasDomainReferencesAsync(domain.Id, cancellation.Token).Returns(true);
        var policy = new ShortLinkDomainDeletionPolicy(links, namespaces);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            policy.EnsureCanDeleteAsync(domain, cancellation.Token));

        exception.Code.ShouldBe(ShortLinkErrorCodes.DomainInUse);
        exception.Data["Origin"].ShouldBe(domain.Origin);
        exception.Data["ReferenceCount"].ShouldBe(0L);
        await namespaces.Received(1).HasDomainReferencesAsync(domain.Id, cancellation.Token);
    }

    [Fact]
    public async Task An_Unreferenced_Ordinary_Domain_Should_Remain_Deletable()
    {
        var links = Substitute.For<IShortLinkDomainRepository>();
        var namespaces = Substitute.For<IShortLinkNamespaceRepository>();
        var domain = new ShortLinkDomain(Guid.NewGuid(), null, "https://go.example.com");

        await new ShortLinkDomainDeletionPolicy(links, namespaces).EnsureCanDeleteAsync(domain);
        await namespaces.Received(1).HasDomainReferencesAsync(domain.Id, Arg.Any<CancellationToken>());
    }

}
