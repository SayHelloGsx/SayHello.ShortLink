using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.WebHost.Account;
using Volo.Abp.Account.Web.Pages.Account;

namespace SayHello.ShortLink.WebHost.Web.Pages.Account;

public class ResendEmailConfirmationModel : AccountPageModel
{
    private readonly IEmailConfirmationAppService _appService;

    [BindProperty(SupportsGet = true)]
    [Required]
    [EmailAddress]
    public string EmailAddress { get; set; } = string.Empty;

    public bool IsSent { get; private set; }

    public ResendEmailConfirmationModel(IEmailConfirmationAppService appService)
    {
        _appService = appService;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _appService.ResendAsync(EmailAddress);
        IsSent = true;
        return Page();
    }
}
