using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SayHello.ShortLink.Admin.ShortLinkDomains;
using SayHello.ShortLink.Admin.Web.Menus;
using SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;
using SayHello.ShortLink.Permissions;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace SayHello.ShortLink.Admin;

public class ShortLinkDomainUiPermissionTests
{
    [Fact]
    public void Domain_Actions_Should_Have_Dedicated_Permissions()
    {
        var permissions = ShortLinkAdminPermissions.GetAll();
        var expected = new[]
        {
            ShortLinkAdminPermissions.Domains.Default,
            ShortLinkAdminPermissions.Domains.Create,
            ShortLinkAdminPermissions.Domains.EnableDisable,
            ShortLinkAdminPermissions.Domains.SetDefault,
            ShortLinkAdminPermissions.Domains.Delete
        };

        expected.All(permissions.Contains).ShouldBeTrue();
        expected.Distinct().Count().ShouldBe(expected.Length);
        ShortLinkAdminMenus.Domains.ShouldBe("ShortLink.Admin.Domains");

        AssertPolicy(nameof(ShortLinkDomainAppService.CreateAsync),
            ShortLinkAdminPermissions.Domains.Create);
        AssertPolicy(nameof(ShortLinkDomainAppService.SetEnabledAsync),
            ShortLinkAdminPermissions.Domains.EnableDisable);
        AssertPolicy(nameof(ShortLinkDomainAppService.SetDefaultAsync),
            ShortLinkAdminPermissions.Domains.SetDefault);
        AssertPolicy(nameof(ShortLinkDomainAppService.DeleteAsync),
            ShortLinkAdminPermissions.Domains.Delete);
    }

    [Fact]
    public async Task Domains_Page_Should_Load_And_Forward_Actions()
    {
        var id = Guid.NewGuid();
        var appService = Substitute.For<IShortLinkDomainAppService>();
        appService.GetListAsync().Returns(
            new ListResultDto<ShortLinkDomainDto>(
            [
                new()
                {
                    Id = id,
                    Origin = "https://go.example.test",
                    IsEnabled = true,
                    IsDefault = true
                }
            ]));
        var authorizationService = Substitute.For<IAuthorizationService>();
        authorizationService
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object?>(),
                Arg.Any<string>())
            .Returns(AuthorizationResult.Success());
        var model = new DomainsModel(appService, authorizationService)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        await model.OnGetAsync();
        model.Items.Single().Id.ShouldBe(id);
        model.CanCreate.ShouldBeTrue();
        model.CanEnableDisable.ShouldBeTrue();
        model.CanSetDefault.ShouldBeTrue();
        model.CanDelete.ShouldBeTrue();

        await model.OnPostSetEnabledAsync(id, false);
        await appService.Received(1).SetEnabledAsync(
            id,
            Arg.Is<SetShortLinkDomainEnabledDto>(
                x => !x.IsEnabled));

        await model.OnPostSetDefaultAsync(id);
        await appService.Received(1).SetDefaultAsync(id);

        await model.OnPostDeleteAsync(id);
        await appService.Received(1).DeleteAsync(id);
    }

    private static void AssertPolicy(string methodName, string policy)
    {
        var attribute = typeof(ShortLinkDomainAppService)
            .GetMethods()
            .Single(x => x.Name == methodName)
            .GetCustomAttribute<AuthorizeAttribute>();
        attribute.ShouldNotBeNull();
        attribute.Policy.ShouldBe(policy);
    }
}
