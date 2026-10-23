using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.Admin.Settings;
using SayHello.ShortLink.Permissions;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.Admin.Web.Pages.Admin.ShortLinks;

[Authorize(ShortLinkAdminPermissions.ManageSettings)]
public class SettingsModel : ShortLinkAdminPageModel
{
    private readonly IShortLinkSettingsAppService _appService;

    [BindProperty]
    public ShortLinkSettingsDto Settings { get; set; } = new();

    public SettingsModel(IShortLinkSettingsAppService appService)
    {
        _appService = appService;
    }

    public async Task OnGetAsync()
    {
        Settings = await _appService.GetAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Settings.IsQuotaExternallyManaged = (await _appService.GetAsync()).IsQuotaExternallyManaged;
            return Page();
        }

        try
        {
            Settings = await _appService.UpdateAsync(Settings);
        }
        catch (BusinessException exception) when (exception.Code is
            ShortLinkErrorCodes.InvalidNamespace or
            ShortLinkErrorCodes.ReservedNamespace or
            ShortLinkErrorCodes.NamespaceAlreadyExists or
            ShortLinkErrorCodes.NamespaceLockUnavailable or
            ShortLinkErrorCodes.DomainConfigurationLockUnavailable)
        {
            var unitOfWorkManager = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();
            if (unitOfWorkManager.Current != null)
            {
                await unitOfWorkManager.Current.RollbackAsync();
            }
            var converter = LazyServiceProvider.LazyGetRequiredService<IExceptionToErrorInfoConverter>();
            ModelState.AddModelError(
                $"{nameof(Settings)}.{nameof(Settings.DefaultNamespace)}",
                converter.Convert(exception).Message ?? exception.Message);
            using var readUnitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            Settings.IsQuotaExternallyManaged = (await _appService.GetAsync()).IsQuotaExternallyManaged;
            await readUnitOfWork.CompleteAsync();
            return Page();
        }
        Alerts.Success(L["SettingsSaved"]);
        return Page();
    }
}
