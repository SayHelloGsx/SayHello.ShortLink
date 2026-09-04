using System;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkTests
{
    [Fact]
    public void State_And_Expiration_Should_Be_Managed_By_Aggregate()
    {
        var now = DateTime.UtcNow;
        var shortLink = new ShortLink(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            "aB3xY9q",
            "https://example.com/path",
            " Example ",
            now.AddHours(1));

        shortLink.Status.ShouldBe(ShortLinkStatus.Active);
        shortLink.Title.ShouldBe("Example");
        shortLink.IsExpired(now).ShouldBeFalse();

        shortLink.Disable();
        shortLink.Status.ShouldBe(ShortLinkStatus.Disabled);

        shortLink.Activate();
        shortLink.Status.ShouldBe(ShortLinkStatus.Active);
        shortLink.IsExpired(now.AddHours(2)).ShouldBeTrue();
    }
}
