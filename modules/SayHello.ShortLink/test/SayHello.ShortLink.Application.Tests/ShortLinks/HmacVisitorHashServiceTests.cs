using System;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.Common.ShortLinks;

public class HmacVisitorHashServiceTests
{
    private readonly HmacVisitorHashService _service = new(
        Options.Create(
            new ShortLinkPrivacyOptions
            {
                VisitorHashKey = "this-is-a-test-only-key-with-32-bytes"
            }));

    [Fact]
    public void Compute_Should_Be_Stable_Within_A_Day_And_Rotate_Daily()
    {
        var firstDay = new DateTime(2026, 9, 4, 1, 0, 0, DateTimeKind.Utc);
        var sameDay = firstDay.AddHours(12);
        var nextDay = firstDay.AddDays(1);

        var firstHash = _service.Compute("203.0.113.10", firstDay);

        _service.Compute("203.0.113.10", sameDay).ShouldBe(firstHash);
        _service.Compute("203.0.113.10", nextDay).ShouldNotBe(firstHash);
        firstHash.Length.ShouldBe(ShortLinkConsts.VisitorHashLength);
        firstHash.ShouldNotContain("203.0.113.10");
    }
}
