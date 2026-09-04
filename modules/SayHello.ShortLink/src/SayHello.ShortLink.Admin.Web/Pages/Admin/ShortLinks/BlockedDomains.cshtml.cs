using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Admin.BlockedDomains;
using SayHello.ShortLink.Permissions;

namespace SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;

[Authorize(ShortLinkAdminPermissions.ManageBlockedDomains)]
public class BlockedDomainsModel : ShortLinkAdminPageModel
{
    private readonly IBlockedDomainAppService _appService;

    public IReadOnlyList<BlockedDomainDto> Items { get; private set; } = [];

    [BindProperty]
    public CreateBlockedDomainDto NewDomain { get; set; } = new();

    public BlockedDomainsModel(IBlockedDomainAppService appService)
    {
        _appService = appService;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        await _appService.CreateAsync(NewDomain);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(
        Guid id,
        string? reason,
        bool isActive,
        string concurrencyStamp)
    {
        await _appService.UpdateAsync(
            id,
            new UpdateBlockedDomainDto
            {
                Reason = reason,
                IsActive = !isActive,
                ConcurrencyStamp = concurrencyStamp
            });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = (await _appService.GetListAsync()).Items;
    }
}
