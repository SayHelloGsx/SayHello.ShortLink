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
}
