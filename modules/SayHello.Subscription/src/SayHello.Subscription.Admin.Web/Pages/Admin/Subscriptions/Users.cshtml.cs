using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;
using SayHello.Subscription.Admin.Users;
using Volo.Abp.Authorization;

namespace SayHello.Subscription.Admin.Web.Pages.Admin.Subscriptions;

[Authorize(SubscriptionAdminPermissions.Users.Default)]
public class UsersModel : SubscriptionAdminPageModel
{
    private readonly IUserSubscriptionAdminAppService _service;
    public bool CanLookup { get; private set; }
    public bool CanAssign { get; private set; }
    public bool CanRevoke { get; private set; }
    public bool CanAdjustExpiration { get; private set; }
    public UsersModel(IUserSubscriptionAdminAppService service) => _service = service;
    public async Task OnGetAsync()
    {
        CanLookup = await AuthorizationService.IsGrantedAsync(SubscriptionAdminPermissions.Users.Lookup);
        CanAssign = await AuthorizationService.IsGrantedAsync(SubscriptionAdminPermissions.Users.Assign);
        CanRevoke = await AuthorizationService.IsGrantedAsync(SubscriptionAdminPermissions.Users.Revoke);
        CanAdjustExpiration = await AuthorizationService.IsGrantedAsync(SubscriptionAdminPermissions.Users.AdjustExpiration);
    }

    public async Task<JsonResult> OnGetListAsync([FromQuery] AdminSubscriptionQueryDto input) => new(await _service.GetListAsync(input));
    public async Task<JsonResult> OnGetItemAsync(Guid id) => new(await _service.GetAsync(id));
    public async Task<JsonResult> OnGetLookupAsync([FromQuery] UserLookupInputDto input)
    {
        await AuthorizationService.CheckAsync(SubscriptionAdminPermissions.Users.Lookup);
        return new(await _service.LookupUsersAsync(input));
    }
    public async Task<JsonResult> OnGetPlansAsync([FromQuery] AdminCatalogQueryDto input)
    {
        await AuthorizationService.CheckAsync(SubscriptionAdminPermissions.Users.Assign);
        return new(await _service.GetPlansAsync(input));
    }
    public async Task<JsonResult> OnGetBundlesAsync([FromQuery] AdminCatalogQueryDto input)
    {
        await AuthorizationService.CheckAsync(SubscriptionAdminPermissions.Users.Assign);
        return new(await _service.GetBundlesAsync(input));
    }
    public async Task<JsonResult> OnGetPreviewAsync(Guid userId, Guid id, bool bundle)
    {
        await AuthorizationService.CheckAsync(SubscriptionAdminPermissions.Users.Assign);
        return new(bundle ? await _service.PreviewBundleAsync(userId, id) : await _service.PreviewPlanAsync(userId, id));
    }
    public Task<IActionResult> OnPostAssignPlanAsync([FromBody] AssignPlanDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Users.Assign, () => _service.AssignPlanAsync(input));
    public Task<IActionResult> OnPostAssignBundleAsync([FromBody] AssignBundleDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Users.Assign, () => _service.AssignBundleAsync(input));
    public Task<IActionResult> OnPostRevokeAsync(Guid id, [FromBody] RevokeSubscriptionDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Users.Revoke, () => _service.RevokeAsync(id, input));
    public Task<IActionResult> OnPostExpirationAsync(Guid id, [FromBody] AdjustExpirationDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Users.AdjustExpiration, () => _service.AdjustExpirationAsync(id, input));
}
