using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

[Authorize(ShortLinkPublicPermissions.Default)]
public class IndexModel : ShortLinkPublicPageModel
{
    private readonly IShortLinkAppService _appService;

    public IReadOnlyList<ShortLinkDto> Items { get; private set; } = [];

    public ShortLinkCapabilitiesDto Capabilities { get; private set; } = new();

    public bool HasCreatePermission { get; private set; }

    public bool CanCreate => HasCreatePermission && Capabilities.IsQuotaGranted &&
        (Capabilities.IsUnlimited || Capabilities.RemainingLinks > 0);

    public bool CanViewStatistics { get; private set; }

    public bool CanUpdate { get; private set; }

    public bool CanDelete { get; private set; }

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

        try
        {
            await _appService.CreateAsync(NewLink);
        }
        catch (BusinessException exception) when (exception.Code is
            ShortLinkErrorCodes.LinkQuotaExceeded or
            ShortLinkErrorCodes.LinkQuotaNotGranted or
            ShortLinkErrorCodes.CreationLockUnavailable)
        {
            var unitOfWorkManager = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();
            if (unitOfWorkManager.Current != null)
            {
                await unitOfWorkManager.Current.RollbackAsync();
            }

            var converter = LazyServiceProvider.LazyGetRequiredService<IExceptionToErrorInfoConverter>();
            ModelState.AddModelError(string.Empty, converter.Convert(exception).Message);
            using var readUnitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            await LoadItemsAsync();
            await readUnitOfWork.CompleteAsync();
            return Page();
        }

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
        Capabilities = await _appService.GetCapabilitiesAsync();
        HasCreatePermission = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Create);
        CanViewStatistics = Capabilities.StatisticsEnabled &&
            await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.ViewStatistics);
        CanUpdate = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Update);
        CanDelete = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Delete);
        var result = await _appService.GetListAsync(
            new GetShortLinksInput
            {
                MaxResultCount = 100,
                Sorting = "creationTime desc"
            });
        Items = result.Items;
    }
}
