using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinkNamespaces;
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
    private readonly IShortLinkNamespaceAppService _namespaceAppService;

    public IReadOnlyList<ShortLinkDto> Items { get; private set; } = [];

    public IReadOnlyList<ShortLinkNamespaceDto> AvailableNamespaces { get; private set; } = [];

    public ShortLinkCapabilitiesDto Capabilities { get; private set; } = new();

    public bool HasCreatePermission { get; private set; }

    public bool CanCreate => HasCreatePermission && Capabilities.IsQuotaGranted &&
        (Capabilities.IsUnlimited || Capabilities.RemainingLinks > 0);

    public bool CanViewStatistics { get; private set; }

    public bool CanUpdate { get; private set; }

    public bool CanDelete { get; private set; }

    public bool HasNamespacePermission { get; private set; }

    public bool CanSelectNamespaces => HasNamespacePermission && Capabilities.NamespacesEnabled;

    public bool CanCreateSelectedPath => CanCreate &&
        (!NewLink.UseRootPath || Capabilities.CanCreateRootLink) &&
        (!NewLink.NamespaceId.HasValue ||
            (!NewLink.UseRootPath && CanSelectNamespaces &&
                AvailableNamespaces.Any(item => item.Id == NewLink.NamespaceId)));

    [BindProperty]
    public CreateShortLinkDto NewLink { get; set; } = new();

    public IndexModel(
        IShortLinkAppService appService,
        IShortLinkNamespaceAppService namespaceAppService)
    {
        _appService = appService;
        _namespaceAppService = namespaceAppService;
    }

    public virtual async Task OnGetAsync()
    {
        await LoadItemsAsync();
    }

    public virtual async Task<IActionResult> OnPostCreateAsync()
    {
        await AuthorizationService.CheckAsync(ShortLinkPublicPermissions.Create);
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
            ShortLinkErrorCodes.CreationLockUnavailable or
            ShortLinkErrorCodes.DomainAccessDenied or
            ShortLinkErrorCodes.DefaultDomainRequired or
            ShortLinkErrorCodes.CustomCodeQuotaNotGranted or
            ShortLinkErrorCodes.CustomCodeQuotaExceeded or
            ShortLinkErrorCodes.RootLinkQuotaNotGranted or
            ShortLinkErrorCodes.RootLinkQuotaExceeded or
            ShortLinkErrorCodes.NamespacesNotGranted or
            ShortLinkErrorCodes.NamespaceAccessDenied or
            ShortLinkErrorCodes.NamespaceLockUnavailable or
            ShortLinkErrorCodes.NamespaceSelectionConflict or
            ShortLinkErrorCodes.DomainConfigurationLockUnavailable)
        {
            var unitOfWorkManager = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();
            if (unitOfWorkManager.Current != null)
            {
                await unitOfWorkManager.Current.RollbackAsync();
            }

            var converter = LazyServiceProvider.LazyGetRequiredService<IExceptionToErrorInfoConverter>();
            ModelState.AddModelError(
                string.Empty,
                converter.Convert(exception).Message ?? exception.Message);
            using var readUnitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            await LoadItemsAsync();
            await readUnitOfWork.CompleteAsync();
            return Page();
        }

        return RedirectToPage();
    }

    public virtual async Task<IActionResult> OnGetNamespacesAsync(string origin)
    {
        await AuthorizationService.CheckAsync(ShortLinkPublicPermissions.Namespaces.Default);
        return new JsonResult(await _namespaceAppService.GetListAsync(origin));
    }

    public virtual async Task<IActionResult> OnPostRefreshNamespacesAsync()
    {
        await AuthorizationService.CheckAsync(ShortLinkPublicPermissions.Create);
        NewLink.NamespaceId = null;
        ModelState.Remove($"{nameof(NewLink)}.{nameof(NewLink.NamespaceId)}");
        await LoadItemsAsync();
        return Page();
    }

    public virtual async Task<IActionResult> OnPostSetStatusAsync(
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

    public virtual async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return RedirectToPage();
    }

    public virtual async Task<IActionResult> OnGetQrCodeAsync(Guid id)
    {
        var qrCode = await _appService.GetQrCodeAsync(id);
        return Content(qrCode.Content, qrCode.ContentType);
    }

    private async Task LoadItemsAsync()
    {
        Capabilities = await _appService.GetCapabilitiesAsync();
        HasCreatePermission = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Create);
        CanViewStatistics = Capabilities.StatisticsLevel != ShortLinkStatisticsLevel.None &&
            await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.ViewStatistics);
        CanUpdate = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Update);
        CanDelete = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Delete);
        HasNamespacePermission = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Namespaces.Default);
        AvailableNamespaces = CanSelectNamespaces &&
            Capabilities.Domains.Any(domain => domain.Origin == NewLink.Origin)
                ? (await _namespaceAppService.GetListAsync(NewLink.Origin)).Items
                : [];
        var result = await _appService.GetListAsync(
            new GetShortLinksInput
            {
                MaxResultCount = 100,
                Sorting = "creationTime desc"
            });
        Items = result.Items;
    }
}
