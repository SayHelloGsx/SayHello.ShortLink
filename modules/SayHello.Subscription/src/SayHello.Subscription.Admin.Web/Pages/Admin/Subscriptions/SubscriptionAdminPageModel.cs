using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SayHello.Subscription.Admin.Localization;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.Http;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace SayHello.Subscription.Admin.Web.Pages.Admin.Subscriptions;

public abstract class SubscriptionAdminPageModel : AbpPageModel
{
    protected SubscriptionAdminPageModel() => LocalizationResourceType = typeof(SubscriptionAdminResource);

    protected virtual async Task<IActionResult> WriteAsync<T>(string permission, Func<Task<T>> action)
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
            var unitOfWork = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>().Current;
            if (unitOfWork != null) await unitOfWork.RollbackAsync();
            return await CreateErrorResultAsync(exception);
        }
    }

    protected virtual Task<IActionResult> WriteAsync(string permission, Func<Task> action) =>
        WriteAsync(permission, async () => { await action(); return new { }; });

    /// <summary>
    /// Returns the same localized error envelope, <see cref="AbpHttpConsts.AbpErrorFormat"/> header, logging and
    /// exception notification as ABP's exception filters, so <c>abp.ajax</c> shows the server message. Unlike
    /// ABP's defaults, handled business and validation failures use 400 and concurrency conflicts use 409.
    /// </summary>
    protected virtual async Task<IActionResult> CreateErrorResultAsync(Exception exception)
    {
        var options = LazyServiceProvider.LazyGetRequiredService<IOptions<AbpExceptionHandlingOptions>>().Value;
        var error = LazyServiceProvider.LazyGetRequiredService<IExceptionToErrorInfoConverter>().Convert(exception, x =>
        {
            x.SendExceptionsDetailsToClients = options.SendExceptionsDetailsToClients;
            x.SendStackTraceToClients = options.SendStackTraceToClients;
            x.SendExceptionDataToClientTypes = options.SendExceptionDataToClientTypes;
        });

        if (options.ShouldLogException(exception))
        {
            Logger.LogException(exception);
        }

        await LazyServiceProvider.LazyGetRequiredService<IExceptionNotifier>()
            .NotifyAsync(new ExceptionNotificationContext(exception));

        Response.Headers[AbpHttpConsts.AbpErrorFormat] = "true";
        return new JsonResult(new RemoteServiceErrorResponse(error))
        {
            StatusCode = exception is AbpDbConcurrencyException
                or BusinessException { Code: SubscriptionErrorCodes.ConcurrencyConflict }
                ? 409
                : 400
        };
    }
}

public abstract class CatalogPageModel : SubscriptionAdminPageModel
{
    public abstract string AreaName { get; }
    public bool CanCreate { get; private set; }
    public bool CanUpdate { get; private set; }
    public bool CanDelete { get; private set; }
    public bool CanPublish { get; private set; }
    public virtual async Task OnGetAsync()
    {
        var prefix = "Subscription.Admin." + AreaName;
        CanCreate = await AuthorizationService.IsGrantedAsync(prefix + ".Create");
        CanUpdate = await AuthorizationService.IsGrantedAsync(prefix + ".Update");
        CanDelete = await AuthorizationService.IsGrantedAsync(prefix + ".Delete");
        CanPublish = await AuthorizationService.IsGrantedAsync(prefix + ".Publish");
    }
}
