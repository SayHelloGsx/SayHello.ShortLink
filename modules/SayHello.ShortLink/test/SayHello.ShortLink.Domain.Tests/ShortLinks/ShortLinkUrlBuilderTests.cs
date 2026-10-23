using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkUrlBuilderTests
{
    private readonly ShortLinkUrlBuilder _builder =
        new(Options.Create(new ShortLinkUrlOptions { BaseUrl = "https://go.example.test" }));

    [Theory]
    [InlineData("", "https://go.example.test/AbC123")]
    [InlineData("s", "https://go.example.test/s/AbC123")]
    [InlineData("S", "https://go.example.test/S/AbC123")]
    [InlineData("Campaign_1-", "https://go.example.test/Campaign_1-/AbC123")]
    public void Url_Should_Include_The_Exact_Stored_Namespace(string pathNamespace, string expected)
    {
        _builder.Build("HTTPS://GO.EXAMPLE.TEST:443/", pathNamespace, "AbC123").ShouldBe(expected);
        _builder.Build(null, pathNamespace, "AbC123").ShouldBe(expected);
    }

    [Fact]
    public void Legacy_Overloads_Should_Remain_Root_Only()
    {
        _builder.Build("AbC123").ShouldBe("https://go.example.test/AbC123");
        _builder.Build("https://other.example.test", "AbC123")
            .ShouldBe("https://other.example.test/AbC123");
    }

    [Fact]
    public void Code_Should_Be_Escaped_As_One_Path_Segment()
    {
        _builder.Build(null, "Campaign", "code/?# %")
            .ShouldBe("https://go.example.test/Campaign/code%2F%3F%23%20%25");
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a b")]
    [InlineData("s\n")]
    [InlineData("含中文")]
    public void Invalid_Namespace_Should_Fail_Instead_Of_Changing_The_Path(string pathNamespace)
    {
        Should.Throw<BusinessException>(() => _builder.Build(null, pathNamespace, "AbC123"))
            .Code.ShouldBe(ShortLinkErrorCodes.InvalidNamespace);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://go.example.test/path")]
    [InlineData("ftp://go.example.test")]
    public void Invalid_Origin_Should_Fail_Explicitly(string origin)
    {
        Should.Throw<AbpException>(() => _builder.Build(origin, "s", "AbC123"));
    }
}
