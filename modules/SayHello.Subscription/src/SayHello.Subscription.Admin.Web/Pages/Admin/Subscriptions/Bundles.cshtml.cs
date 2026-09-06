using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;

namespace SayHello.Subscription.Admin.Web.Pages.Admin.Subscriptions;

[Authorize(SubscriptionAdminPermissions.Bundles.Default)]
public class BundlesModel : CatalogPageModel
{
    public override string AreaName => "Bundles";
    private readonly IBundleAdminAppService _service;
    public BundlesModel(IBundleAdminAppService service) => _service = service;
    public async Task<JsonResult> OnGetListAsync([FromQuery] AdminCatalogQueryDto input) => new(await _service.GetListAsync(input));
    public async Task<JsonResult> OnGetItemAsync(Guid id) => new(await _service.GetAsync(id));
    public async Task<JsonResult> OnGetOptionsAsync([FromQuery] AdminCatalogQueryDto input) => new(await _service.GetPlansAsync(input));
    public Task<IActionResult> OnPostCreateAsync([FromBody] CreateBundleDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Bundles.Create, () => _service.CreateAsync(input));
    public Task<IActionResult> OnPostUpdateAsync(Guid id, [FromBody] UpdateBundleDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Bundles.Update, () => _service.UpdateAsync(id, input));
    public Task<IActionResult> OnPostStateAsync(Guid id, [FromBody] CatalogStateInputDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Bundles.Publish, () => _service.SetStateAsync(id, input));
    public Task<IActionResult> OnPostDeleteAsync(Guid id, [FromBody] VersionInputDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Bundles.Delete, () => _service.DeleteAsync(id, input));
}
