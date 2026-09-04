using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.WebHost.Account;
using Volo.Abp.Account.Web.Pages.Account;

namespace SayHello.ShortLink.WebHost.Web.Pages.Account;

public class ConfirmEmailModel : AccountPageModel
{
    private readonly IEmailConfirmationAppService _appService;

    public bool IsConfirmed { get; private set; }

    public ConfirmEmailModel(IEmailConfirmationAppService appService)
    {
        _appService = appService;
    }

    public async Task OnGetAsync(Guid userId, string token)
    {
        IsConfirmed = await _appService.ConfirmAsync(userId, token);
    }
}
