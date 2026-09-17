using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SayHello.ShortLink.Admin.ShortLinkDomains;
using SayHello.ShortLink.Permissions;

namespace SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;

[Authorize(ShortLinkAdminPermissions.Domains.Default)]
public class DomainsModel : ShortLinkAdminPageModel
{
    private readonly IShortLinkDomainAppService _appService;
    private readonly IAuthorizationService _authorizationService;

    public IReadOnlyList<ShortLinkDomainDto> Items { get; private set; } = [];

    [BindProperty]
    public CreateShortLinkDomainDto NewDomain { get; set; } = new();

    public bool CanCreate { get; private set; }

    public bool CanEnableDisable { get; private set; }

    public bool CanSetDefault { get; private set; }

    public bool CanDelete { get; private set; }

    public DomainsModel(
        IShortLinkDomainAppService appService,
        IAuthorizationService authorizationService)
    {
        _appService = appService;
        _authorizationService = authorizationService;
    }

    public Task OnGetAsync()
    {
        return LoadAsync();
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

    public async Task<IActionResult> OnPostSetEnabledAsync(
        Guid id,
        bool isEnabled)
    {
        await _appService.SetEnabledAsync(
            id,
            new SetShortLinkDomainEnabledDto
            {
                IsEnabled = isEnabled
            });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetDefaultAsync(Guid id)
    {
        await _appService.SetDefaultAsync(id);
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
        var user = User ?? new ClaimsPrincipal();
        CanCreate = await IsGrantedAsync(user, ShortLinkAdminPermissions.Domains.Create);
        CanEnableDisable = await IsGrantedAsync(
            user,
            ShortLinkAdminPermissions.Domains.EnableDisable);
        CanSetDefault = await IsGrantedAsync(
            user,
            ShortLinkAdminPermissions.Domains.SetDefault);
        CanDelete = await IsGrantedAsync(user, ShortLinkAdminPermissions.Domains.Delete);
    }

    private async Task<bool> IsGrantedAsync(
        ClaimsPrincipal user,
        string permission)
    {
        return (await _authorizationService.AuthorizeAsync(user, permission)).Succeeded;
    }
}
