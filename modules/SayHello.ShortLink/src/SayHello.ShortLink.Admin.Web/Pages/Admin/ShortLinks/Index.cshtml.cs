using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Admin.ShortLinks;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;

[Authorize(ShortLinkAdminPermissions.ManageAllLinks)]
public class IndexModel : ShortLinkAdminPageModel
{
    private readonly IShortLinkAdministrationAppService _appService;

    public IReadOnlyList<ShortLinkDto> Items { get; private set; } = [];

    public IndexModel(IShortLinkAdministrationAppService appService)
    {
        _appService = appService;
    }

    public async Task OnGetAsync()
    {
        var result = await _appService.GetListAsync(
            new GetShortLinksInput
            {
                MaxResultCount = 100,
                Sorting = "creationTime desc"
            });
        Items = result.Items;
    }

    public async Task<IActionResult> OnPostSetStatusAsync(
        Guid id,
        ShortLinkStatus status,
        string concurrencyStamp)
    {
        await _appService.SetStatusAsync(
            id,
            new SetShortLinkStatusDto
            {
                Status = status,
                ConcurrencyStamp = concurrencyStamp
            });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return RedirectToPage();
    }
}
