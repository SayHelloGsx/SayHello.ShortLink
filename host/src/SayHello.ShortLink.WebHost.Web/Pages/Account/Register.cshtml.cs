using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.Identity;

namespace SayHello.ShortLink.WebHost.Web.Pages.Account;

public class RegisterModel : Volo.Abp.Account.Web.Pages.Account.RegisterModel
{
    private bool _localRegistrationSucceeded;

    public RegisterModel(
        IAccountAppService accountAppService,
        IAuthenticationSchemeProvider schemeProvider,
        IOptions<AbpAccountOptions> accountOptions,
        IdentityDynamicClaimsPrincipalContributorCache identityDynamicClaimsPrincipalContributorCache)
        : base(
            accountAppService,
            schemeProvider,
            accountOptions,
            identityDynamicClaimsPrincipalContributorCache)
    {
    }

    public override async Task<IActionResult> OnPostAsync()
    {
        var result = await base.OnPostAsync();
        if (!_localRegistrationSucceeded)
        {
            return result;
        }

        return RedirectToPage(
            "./EmailConfirmationSent",
            new
            {
                emailAddress = Input.EmailAddress,
                returnUrl = ReturnUrl
            });
    }

    protected override async Task RegisterLocalUserAsync()
    {
        ValidateModel();

        await AccountAppService.RegisterAsync(
            new RegisterDto
            {
                AppName = "MVC",
                EmailAddress = Input.EmailAddress,
                Password = Input.Password,
                UserName = Input.UserName
            });

        _localRegistrationSucceeded = true;
    }
}
