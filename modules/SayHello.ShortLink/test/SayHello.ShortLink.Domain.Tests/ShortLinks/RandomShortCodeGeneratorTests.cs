using System.Linq;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class RandomShortCodeGeneratorTests
{
    [Fact]
    public void Generate_Should_Create_Base62_Code_With_Requested_Length()
    {
        var generator = new RandomShortCodeGenerator();

        var codes = Enumerable.Range(0, 100)
            .Select(_ => generator.Generate(ShortLinkConsts.GeneratedCodeLength))
            .ToList();

        codes.ShouldAllBe(code => code.Length == ShortLinkConsts.GeneratedCodeLength);
        codes.ShouldAllBe(code => code.All(char.IsLetterOrDigit));
        codes.Distinct().Count().ShouldBeGreaterThan(95);
    }
}
