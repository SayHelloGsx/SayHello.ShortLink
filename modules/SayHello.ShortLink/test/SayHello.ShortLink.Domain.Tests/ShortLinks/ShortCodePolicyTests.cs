using Shouldly;
using Volo.Abp;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortCodePolicyTests
{
    private readonly ShortCodePolicy _policy = new();

    [Theory]
    [InlineData("abc")]
    [InlineData("AbC_123")]
    [InlineData("custom-code")]
    public void ValidateCustomCode_Should_Preserve_Valid_Code(string code)
    {
        _policy.ValidateCustomCode(code).ShouldBe(code);
    }

    [Theory]
    [InlineData(" abc ", "abc")]
    [InlineData("\tAbC_123\r\n", "AbC_123")]
    [InlineData(" custom-code\n", "custom-code")]
    public void Strict_Format_Reusage_Should_Preserve_Custom_Code_Trimming(string input, string expected)
    {
        _policy.ValidateCustomCode(input).ShouldBe(expected);
    }

    [Fact]
    public void Custom_Code_Length_Boundaries_Should_Match_The_Shared_Strict_Format()
    {
        foreach (var length in new[] { ShortLinkConsts.MinCodeLength, ShortLinkConsts.MaxCodeLength })
        {
            var code = new string('A', length);
            ShortCodeFormat.IsValid(code).ShouldBeTrue();
            _policy.ValidateCustomCode(code).ShouldBe(code);
        }
        foreach (var length in new[] { ShortLinkConsts.MinCodeLength - 1, ShortLinkConsts.MaxCodeLength + 1 })
        {
            var code = new string('A', length);
            ShortCodeFormat.IsValid(code).ShouldBeFalse();
            Should.Throw<BusinessException>(() => _policy.ValidateCustomCode(code))
                .Code.ShouldBe(ShortLinkErrorCodes.InvalidCode);
        }
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("has.dot")]
    [InlineData("含中文")]
    public void ValidateCustomCode_Should_Reject_Invalid_Code(string code)
    {
        var exception = Should.Throw<BusinessException>(() => _policy.ValidateCustomCode(code));
        exception.Code.ShouldBe(ShortLinkErrorCodes.InvalidCode);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("API")]
    [InlineData("Swagger")]
    public void ValidateCustomCode_Should_Reject_Reserved_Code(string code)
    {
        var exception = Should.Throw<BusinessException>(() => _policy.ValidateCustomCode(code));
        exception.Code.ShouldBe(ShortLinkErrorCodes.ReservedCode);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("API")]
    [InlineData("Swagger")]
    public void Non_Root_Paths_Should_Allow_Protected_Prefixes_As_Codes(string code)
    {
        _policy.ValidateCustomCode(code, allowReserved: true).ShouldBe(code);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("tenantmanagement")]
    [InlineData("settingmanagement")]
    [InlineData("featuremanagement")]
    [InlineData("abppermissionmanagement")]
    [InlineData("signout-oidc")]
    public void New_Protected_Namespaces_Should_Not_Change_The_Legacy_Root_Code_Policy(string code)
    {
        _policy.ValidateCustomCode(code).ShouldBe(code);
        _policy.ValidateCustomCode(code.ToUpperInvariant()).ShouldBe(code.ToUpperInvariant());
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a/b")]
    [InlineData("has space")]
    [InlineData("a\nbc")]
    [InlineData("abc\0")]
    [InlineData("含中文")]
    public void Allowing_Protected_Codes_Should_Not_Disable_Code_Syntax(string code)
    {
        Should.Throw<BusinessException>(() => _policy.ValidateCustomCode(code, allowReserved: true))
            .Code.ShouldBe(ShortLinkErrorCodes.InvalidCode);
    }
}
