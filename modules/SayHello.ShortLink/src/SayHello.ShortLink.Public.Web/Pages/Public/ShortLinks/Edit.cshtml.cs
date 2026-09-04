using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

[Authorize(ShortLinkPublicPermissions.Update)]
public class EditModel : ShortLinkPublicPageModel
{
    private readonly IShortLinkAppService _appService;

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    [BindProperty]
    public UpdateShortLinkDto Input { get; set; } = new();

    public EditModel(IShortLinkAppService appService)
    {
        _appService = appService;
    }

    public async Task OnGetAsync(Guid id)
    {
        Id = id;
        var item = await _appService.GetAsync(id);
        Code = item.Code;
        Input = new UpdateShortLinkDto
        {
            TargetUrl = item.TargetUrl,
            Title = item.Title,
            ExpiresAt = item.ExpiresAt,
            ConcurrencyStamp = item.ConcurrencyStamp
        };
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            Id = id;
            Code = (await _appService.GetAsync(id)).Code;
            return Page();
        }

        await _appService.UpdateAsync(id, Input);
        return RedirectToPage("./Index");
    }
}
