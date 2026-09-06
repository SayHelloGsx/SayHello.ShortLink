using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;

namespace SayHello.Subscription.Admin.Web.Pages.Admin.Subscriptions;

[Authorize(SubscriptionAdminPermissions.Plans.Default)]
public class PlansModel : CatalogPageModel
{
    public override string AreaName => "Plans";
    private readonly IPlanAdminAppService _service;
    public PlansModel(IPlanAdminAppService service) => _service = service;
    public async Task<JsonResult> OnGetListAsync([FromQuery] AdminCatalogQueryDto input) => new(await _service.GetListAsync(input));
    public async Task<JsonResult> OnGetItemAsync(Guid id) => new(await _service.GetAsync(id));
    public async Task<JsonResult> OnGetOptionsAsync([FromQuery] AdminCatalogQueryDto input) => new(await _service.GetProductsAsync(input));
    public async Task<JsonResult> OnGetDefinitionAsync(Guid productId) => new(await _service.GetDefinitionAsync(productId));
    public Task<IActionResult> OnPostCreateAsync([FromBody] CreatePlanDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Plans.Create, () => _service.CreateAsync(input));
    public Task<IActionResult> OnPostUpdateAsync(Guid id, [FromBody] UpdatePlanDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Plans.Update, () => _service.UpdateAsync(id, input));
    public Task<IActionResult> OnPostStateAsync(Guid id, [FromBody] CatalogStateInputDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Plans.Publish, () => _service.SetStateAsync(id, input));
    public Task<IActionResult> OnPostDeleteAsync(Guid id, [FromBody] VersionInputDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Plans.Delete, () => _service.DeleteAsync(id, input));
}
