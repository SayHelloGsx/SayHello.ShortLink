using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Admin.Settings;
using SayHello.ShortLink.Permissions;

namespace SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;

[Authorize(ShortLinkAdminPermissions.ManageSettings)]
public class SettingsModel : ShortLinkAdminPageModel
{
    private readonly IShortLinkSettingsAppService _appService;

    [BindProperty]
    public ShortLinkSettingsDto Settings { get; set; } = new();

    public SettingsModel(IShortLinkSettingsAppService appService)
    {
        _appService = appService;
    }

    public async Task OnGetAsync()
    {
        Settings = await _appService.GetAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Settings = await _appService.UpdateAsync(Settings);
        Alerts.Success(L["SettingsSaved"]);
        return Page();
    }
}
