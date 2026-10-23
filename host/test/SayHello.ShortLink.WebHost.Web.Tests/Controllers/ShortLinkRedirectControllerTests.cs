using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.Public.Web.Controllers;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.WebHost.Controllers;

public class ShortLinkRedirectControllerTests
{
    [Theory]
    [InlineData(ShortLinkResolutionStatus.Found, false)]
    [InlineData(ShortLinkResolutionStatus.Found, true)]
    [InlineData(ShortLinkResolutionStatus.NotFound, false)]
    [InlineData(ShortLinkResolutionStatus.NotFound, true)]
    [InlineData(ShortLinkResolutionStatus.Gone, false)]
    [InlineData(ShortLinkResolutionStatus.Gone, true)]
    [InlineData(ShortLinkResolutionStatus.Blocked, false)]
    [InlineData(ShortLinkResolutionStatus.Blocked, true)]
    public async Task Namespaced_Resolution_Should_Preserve_Status_And_Not_Record_Head(
        ShortLinkResolutionStatus status, bool head)
    {
        var appService = Substitute.For<IShortLinkRedirectAppService>();
        appService.ResolveAsync(
                "https://short.example.test", "s", "api", Arg.Any<RecordShortLinkVisitDto?>())
            .Returns(new ShortLinkResolutionDto
            {
                Status = status,
                TargetUrl = "https://destination.example.test/path"
            });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = head ? HttpMethods.Head : HttpMethods.Get;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("short.example.test");
        var controller = new ShortLinkRedirectController(appService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.ResolveNamespacedAsync("s", "api");

        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        if (status == ShortLinkResolutionStatus.Found)
        {
            result.ShouldBeOfType<RedirectResult>().Url.ShouldBe("https://destination.example.test/path");
        }
        else
        {
            result.ShouldBeOfType<ViewResult>().ViewName.ShouldBe(status switch
            {
                ShortLinkResolutionStatus.Gone => "Gone",
                ShortLinkResolutionStatus.Blocked => "Blocked",
                _ => "NotFound"
            });
            httpContext.Response.StatusCode.ShouldBe(status switch
            {
                ShortLinkResolutionStatus.Gone => StatusCodes.Status410Gone,
                ShortLinkResolutionStatus.Blocked => StatusCodes.Status451UnavailableForLegalReasons,
                _ => StatusCodes.Status404NotFound
            });
        }
        await appService.Received(1).ResolveAsync(
            "https://short.example.test", "s", "api",
            Arg.Is<RecordShortLinkVisitDto?>(visit => head ? visit == null : visit != null));
        await appService.DidNotReceive().ResolveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RecordShortLinkVisitDto?>());
    }

    [Fact]
    public async Task ResolveAsync_Should_Return_451_View_For_A_Blocked_Destination()
    {
        var appService = Substitute.For<IShortLinkRedirectAppService>();
        appService.ResolveAsync(
                "https://short.example.test",
                "Blocked1",
                Arg.Any<RecordShortLinkVisitDto?>())
            .Returns(
                new ShortLinkResolutionDto
                {
                    Status = ShortLinkResolutionStatus.Blocked,
                    BlockedDomain = "blocked.example",
                    BlockedReason = "Unsafe destination"
                });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("short.example.test");
        var controller = new ShortLinkRedirectController(appService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var action = await controller.ResolveAsync("Blocked1");

        httpContext.Response.StatusCode
            .ShouldBe(StatusCodes.Status451UnavailableForLegalReasons);
        var view = action.ShouldBeOfType<ViewResult>();
        view.ViewName.ShouldBe("Blocked");
        var model = view.Model.ShouldBeOfType<ShortLinkResolutionDto>();
        model.BlockedDomain.ShouldBe("blocked.example");
        model.BlockedReason.ShouldBe("Unsafe destination");
    }
}
