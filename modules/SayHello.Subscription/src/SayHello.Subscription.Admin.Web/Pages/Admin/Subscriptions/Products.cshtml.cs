using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;

namespace SayHello.Subscription.Admin.Web.Pages.Admin.Subscriptions;

[Authorize(SubscriptionAdminPermissions.Products.Default)]
public class ProductsModel : CatalogPageModel
{
    public override string AreaName => "Products";
    private readonly IProductAdminAppService _service;
    public ProductsModel(IProductAdminAppService service) => _service = service;
    public async Task<JsonResult> OnGetListAsync([FromQuery] AdminCatalogQueryDto input) => new(await _service.GetListAsync(input));
    public async Task<JsonResult> OnGetItemAsync(Guid id) => new(await _service.GetAsync(id));
    public async Task<JsonResult> OnGetDefinitionsAsync() => new(await _service.GetDefinitionsAsync());
    public Task<IActionResult> OnPostCreateAsync([FromBody] CreateProductDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Products.Create, () => _service.CreateAsync(input));
    public Task<IActionResult> OnPostUpdateAsync(Guid id, [FromBody] UpdateProductDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Products.Update, () => _service.UpdateAsync(id, input));
    public Task<IActionResult> OnPostStateAsync(Guid id, [FromBody] CatalogStateInputDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Products.Publish, () => _service.SetStateAsync(id, input));
    public Task<IActionResult> OnPostDeleteAsync(Guid id, [FromBody] VersionInputDto input) =>
        WriteAsync(SubscriptionAdminPermissions.Products.Delete, () => _service.DeleteAsync(id, input));
}
