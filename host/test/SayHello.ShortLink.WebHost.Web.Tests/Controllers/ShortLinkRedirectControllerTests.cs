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
    [Fact]
    public async Task ResolveAsync_Should_Return_451_View_For_A_Blocked_Destination()
    {
        var appService = Substitute.For<IShortLinkRedirectAppService>();
        appService.ResolveAsync("Blocked1", Arg.Any<RecordShortLinkVisitDto?>())
            .Returns(
                new ShortLinkResolutionDto
                {
                    Status = ShortLinkResolutionStatus.Blocked,
                    BlockedDomain = "blocked.example",
                    BlockedReason = "Unsafe destination"
                });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
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
