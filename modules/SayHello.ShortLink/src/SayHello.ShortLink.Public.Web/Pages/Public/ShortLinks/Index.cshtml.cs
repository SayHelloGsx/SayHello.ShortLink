using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

[Authorize(ShortLinkPublicPermissions.Default)]
public class IndexModel : ShortLinkPublicPageModel
{
    private readonly IShortLinkAppService _appService;

    public IReadOnlyList<ShortLinkDto> Items { get; private set; } = [];

    [BindProperty]
    public CreateShortLinkDto NewLink { get; set; } = new();

    public IndexModel(IShortLinkAppService appService)
    {
        _appService = appService;
    }

    public async Task OnGetAsync()
    {
        await LoadItemsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadItemsAsync();
            return Page();
        }

        await _appService.CreateAsync(NewLink);
        return RedirectToPage();
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

    public async Task<IActionResult> OnGetQrCodeAsync(Guid id)
    {
        var qrCode = await _appService.GetQrCodeAsync(id);
        return Content(qrCode.Content, qrCode.ContentType);
    }

    private async Task LoadItemsAsync()
    {
        var result = await _appService.GetListAsync(
            new GetShortLinksInput
            {
                MaxResultCount = 100,
                Sorting = "creationTime desc"
            });
        Items = result.Items;
    }
}
