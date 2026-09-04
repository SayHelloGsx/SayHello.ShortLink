using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Admin.BlockedDomains;
using SayHello.ShortLink.Permissions;
using Volo.Abp.Content;

namespace SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;

[Authorize(ShortLinkAdminPermissions.ManageBlockedDomains)]
public class BlockedDomainsModel : ShortLinkAdminPageModel
{
    private readonly IBlockedDomainAppService _appService;

    public IReadOnlyList<BlockedDomainDto> Items { get; private set; } = [];

    [BindProperty]
    public CreateBlockedDomainDto NewDomain { get; set; } = new();

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public BlockedDomainImportResultDto? ImportResult { get; private set; }

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

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (CsvFile is null)
        {
            ModelState.AddModelError(nameof(CsvFile), L["BlockedDomainImport:FileRequired"]);
            await LoadAsync();
            return Page();
        }

        using var content = new RemoteStreamContent(
            CsvFile.OpenReadStream(),
            CsvFile.FileName,
            CsvFile.ContentType,
            CsvFile.Length);
        ImportResult = await _appService.ImportAsync(content);
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Items = (await _appService.GetListAsync()).Items;
    }
}
