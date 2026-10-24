using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinkNamespaces;
using SayHello.ShortLink.Public.ShortLinks;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Data;
using Volo.Abp.Http;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

[Authorize(ShortLinkPublicPermissions.Namespaces.Default)]
public class NamespacesModel : ShortLinkPublicPageModel
{
    private readonly IShortLinkNamespaceAppService _namespaceAppService;
    private readonly IShortLinkAppService _linkAppService;

    public IReadOnlyList<ShortLinkNamespaceDto> Items { get; private set; } = [];

    public ShortLinkCapabilitiesDto? Capabilities { get; private set; }

    public bool CanViewLinks { get; private set; }

    public bool HasCreatePermission { get; private set; }

    public bool CanCreate => HasCreatePermission && (Capabilities is null || Capabilities.CanCreateNamespace);

    public bool CanDelete { get; private set; }

    [BindProperty]
    public CreateShortLinkNamespaceDto NewNamespace { get; set; } = new();

    public NamespacesModel(
        IShortLinkNamespaceAppService namespaceAppService,
        IShortLinkAppService linkAppService)
    {
        _namespaceAppService = namespaceAppService;
        _linkAppService = linkAppService;
    }

    public virtual Task OnGetAsync() => LoadAsync();

    public virtual async Task<IActionResult> OnPostCreateAsync()
    {
        await AuthorizationService.CheckAsync(ShortLinkPublicPermissions.Namespaces.Create);
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        try
        {
            await _namespaceAppService.CreateAsync(NewNamespace);
        }
        catch (BusinessException exception) when (exception.Code is
            ShortLinkErrorCodes.InvalidNamespace or
            ShortLinkErrorCodes.ReservedNamespace or
            ShortLinkErrorCodes.NamespaceAlreadyExists or
            ShortLinkErrorCodes.NamespacesNotGranted or
            ShortLinkErrorCodes.NamespaceQuotaNotGranted or
            ShortLinkErrorCodes.NamespaceQuotaExceeded or
            ShortLinkErrorCodes.NamespaceLockUnavailable or
            ShortLinkErrorCodes.CreationLockUnavailable or
            ShortLinkErrorCodes.DomainConfigurationLockUnavailable or
            ShortLinkErrorCodes.InvalidDomainOrigin or
            ShortLinkErrorCodes.DomainAccessDenied or
            ShortLinkErrorCodes.DefaultDomainRequired or
            ShortLinkErrorCodes.EmailNotConfirmed)
        {
            var unitOfWorkManager = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();
            if (unitOfWorkManager.Current != null)
            {
                await unitOfWorkManager.Current.RollbackAsync();
            }
            var converter = LazyServiceProvider.LazyGetRequiredService<IExceptionToErrorInfoConverter>();
            ModelState.AddModelError(string.Empty, converter.Convert(exception).Message ?? exception.Message);
            using var readUnitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            await LoadAsync();
            await readUnitOfWork.CompleteAsync();
            return Page();
        }

        return RedirectToPage();
    }

    public virtual async Task<IActionResult> OnPostDeleteAsync(Guid id, string concurrencyStamp)
    {
        await AuthorizationService.CheckAsync(ShortLinkPublicPermissions.Namespaces.Delete);
        var unitOfWorkManager = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();
        try
        {
            await _namespaceAppService.DeleteAsync(
                id,
                new DeleteShortLinkNamespaceDto { ConcurrencyStamp = concurrencyStamp });
            if (unitOfWorkManager.Current != null)
            {
                await unitOfWorkManager.Current.SaveChangesAsync();
            }
            return IsAjaxRequest() ? new JsonResult(new { }) : RedirectToPage();
        }
        catch (Exception exception) when (exception is BusinessException or AbpDbConcurrencyException or AbpValidationException)
        {
            if (unitOfWorkManager.Current != null)
            {
                await unitOfWorkManager.Current.RollbackAsync();
            }
            var converter = LazyServiceProvider.LazyGetRequiredService<IExceptionToErrorInfoConverter>();
            var error = converter.Convert(exception);
            var statusCode = exception is AbpDbConcurrencyException ? 409 : 400;
            if (IsAjaxRequest())
            {
                return new JsonResult(new RemoteServiceErrorResponse(error)) { StatusCode = statusCode };
            }

            ModelState.AddModelError(string.Empty, error.Message ?? exception.Message);
            Response.StatusCode = statusCode;
            using var readUnitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            await LoadAsync();
            await readUnitOfWork.CompleteAsync();
            return Page();
        }
    }

    private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private async Task LoadAsync()
    {
        Items = (await _namespaceAppService.GetListAsync()).Items;
        CanViewLinks = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Default);
        HasCreatePermission = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Namespaces.Create);
        CanDelete = await AuthorizationService.IsGrantedAsync(ShortLinkPublicPermissions.Namespaces.Delete);
        Capabilities = CanViewLinks ? await _linkAppService.GetCapabilitiesAsync() : null;
    }
}
