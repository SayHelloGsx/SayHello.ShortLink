using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

[Authorize(ShortLinkPublicPermissions.Update)]
public class EditModel : ShortLinkPublicPageModel
{
    private readonly IShortLinkAppService _appService;

    public Guid Id { get; private set; }

    public ShortLinkDto Link { get; private set; } = new();

    public string Code => Link.Code;

    [BindProperty]
    public UpdateShortLinkDto Input { get; set; } = new();

    public EditModel(IShortLinkAppService appService)
    {
        _appService = appService;
    }

    public virtual async Task OnGetAsync(Guid id)
    {
        Id = id;
        var item = await _appService.GetAsync(id);
        Link = item;
        Input = new UpdateShortLinkDto
        {
            TargetUrl = item.TargetUrl,
            Title = item.Title,
            ExpiresAt = item.ExpiresAt,
            ConcurrencyStamp = item.ConcurrencyStamp
        };
    }

    public virtual async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            Id = id;
            Link = await _appService.GetAsync(id);
            return Page();
        }

        await _appService.UpdateAsync(id, Input);
        return RedirectToPage("./Index");
    }
}
