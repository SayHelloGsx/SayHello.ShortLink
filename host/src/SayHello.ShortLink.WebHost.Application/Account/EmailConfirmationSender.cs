using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using SayHello.ShortLink.WebHost.Localization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.UI.Navigation.Urls;

namespace SayHello.ShortLink.WebHost.Account;

public class EmailConfirmationSender : IEmailConfirmationSender, ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IAppUrlProvider _appUrlProvider;
    private readonly IStringLocalizer<WebHostResource> _localizer;

    public EmailConfirmationSender(
        IdentityUserManager userManager,
        IEmailSender emailSender,
        IAppUrlProvider appUrlProvider,
        IStringLocalizer<WebHostResource> localizer)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _appUrlProvider = appUrlProvider;
        _localizer = localizer;
    }

    public async Task SendAsync(IdentityUser user, string appName)
    {
        if (user.Email.IsNullOrWhiteSpace())
        {
            throw new AbpException($"User '{user.Id}' does not have an email address.");
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var rootUrl = await _appUrlProvider.GetUrlAsync(appName);
        var link = $"{rootUrl.TrimEnd('/')}/Account/ConfirmEmail" +
                   $"?userId={user.Id:D}&token={Uri.EscapeDataString(token)}";
        var encodedLink = HtmlEncoder.Default.Encode(link);
        var body = $"""
            <p>{HtmlEncoder.Default.Encode(_localizer["EmailConfirmation:Hello", user.UserName])}</p>
            <p>{HtmlEncoder.Default.Encode(_localizer["EmailConfirmation:Instructions"])}</p>
            <p><a href="{encodedLink}">{HtmlEncoder.Default.Encode(_localizer["EmailConfirmation:Action"])}</a></p>
            <p>{HtmlEncoder.Default.Encode(_localizer["EmailConfirmation:Expiry"])}</p>
            """;

        await _emailSender.SendAsync(
            user.Email,
            _localizer["EmailConfirmation:Subject"],
            body);
    }
}
