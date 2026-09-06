using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Admin.Localization;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Http;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace SayHello.Subscription.Admin.Web.Pages.Admin.Subscriptions;

public abstract class SubscriptionAdminPageModel : AbpPageModel
{
    protected SubscriptionAdminPageModel() => LocalizationResourceType = typeof(SubscriptionAdminResource);

    protected async Task<IActionResult> WriteAsync<T>(string permission, Func<Task<T>> action)
    {
        await AuthorizationService.CheckAsync(permission);
        try
        {
            ValidateModel();
            var result = await action();
            var unitOfWork = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>().Current;
            if (unitOfWork != null) await unitOfWork.SaveChangesAsync();
            return new JsonResult(result);
        }
        catch (Exception exception) when (exception is BusinessException or AbpDbConcurrencyException or AbpValidationException)
        {
            // Razor handlers return the same localized ABP error envelope as HTTP controllers.
            var unitOfWork = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>().Current;
            if (unitOfWork != null) await unitOfWork.RollbackAsync();
            var converter = LazyServiceProvider.LazyGetRequiredService<IExceptionToErrorInfoConverter>();
            return new JsonResult(new RemoteServiceErrorResponse(converter.Convert(exception)))
            {
                StatusCode = exception is AbpDbConcurrencyException ? 409 : 400
            };
        }
    }

    protected Task<IActionResult> WriteAsync(string permission, Func<Task> action) =>
        WriteAsync(permission, async () => { await action(); return new { }; });
}

public abstract class CatalogPageModel : SubscriptionAdminPageModel
{
    public abstract string AreaName { get; }
    public bool CanCreate { get; private set; }
    public bool CanUpdate { get; private set; }
    public bool CanDelete { get; private set; }
    public bool CanPublish { get; private set; }
    public async Task OnGetAsync()
    {
        var prefix = "Subscription.Admin." + AreaName;
        CanCreate = await AuthorizationService.IsGrantedAsync(prefix + ".Create");
        CanUpdate = await AuthorizationService.IsGrantedAsync(prefix + ".Update");
        CanDelete = await AuthorizationService.IsGrantedAsync(prefix + ".Delete");
        CanPublish = await AuthorizationService.IsGrantedAsync(prefix + ".Publish");
    }
}
