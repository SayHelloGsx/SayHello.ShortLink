using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Account.Web.Pages.Account;

namespace SayHello.ShortLink.WebHost.Web.Pages.Account;

public class EmailConfirmationSentModel : AccountPageModel
{
    [BindProperty(SupportsGet = true)]
    public string EmailAddress { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }
}
