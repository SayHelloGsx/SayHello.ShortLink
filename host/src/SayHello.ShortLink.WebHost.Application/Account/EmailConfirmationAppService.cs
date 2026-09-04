using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using SayHello.ShortLink.WebHost.Localization;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;

namespace SayHello.ShortLink.WebHost.Account;

[AllowAnonymous]
public class EmailConfirmationAppService :
    ApplicationService,
    IEmailConfirmationAppService
{
    private readonly IdentityUserManager _userManager;
    private readonly IEmailConfirmationSender _emailConfirmationSender;

    public EmailConfirmationAppService(
        IdentityUserManager userManager,
        IEmailConfirmationSender emailConfirmationSender)
    {
        _userManager = userManager;
        _emailConfirmationSender = emailConfirmationSender;
        LocalizationResource = typeof(WebHostResource);
    }

    public async Task<bool> ConfirmAsync(Guid userId, string token)
    {
        if (token.IsNullOrWhiteSpace())
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString("D"));
        if (user is null)
        {
            return false;
        }

        if (user.EmailConfirmed)
        {
            return true;
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task ResendAsync(string emailAddress)
    {
        if (emailAddress.IsNullOrWhiteSpace())
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(emailAddress);
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        await _emailConfirmationSender.SendAsync(user, "MVC");
    }
}
