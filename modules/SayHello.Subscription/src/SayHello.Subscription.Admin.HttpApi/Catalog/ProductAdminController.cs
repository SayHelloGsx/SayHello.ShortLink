using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Admin.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.Subscription.Admin.Catalog;

[RemoteService(Name = SubscriptionAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SubscriptionAdminRemoteServiceConsts.ModuleName)]
[Route("api/subscription/admin/products")]
[Authorize(SubscriptionAdminPermissions.Products.Default)]
public class ProductAdminController : AbpControllerBase, IProductAdminAppService
{
    private readonly IProductAdminAppService _service;
    public ProductAdminController(IProductAdminAppService service) => _service = service;
    [HttpGet]
    public Task<PagedResultDto<AdminProductDto>> GetListAsync([FromQuery] AdminCatalogQueryDto input) => _service.GetListAsync(input);
    [HttpGet("{id:guid}")]
    public Task<AdminProductDto> GetAsync(Guid id) => _service.GetAsync(id);
    [HttpGet("definitions")]
    public Task<ListResultDto<RegisteredProductDto>> GetDefinitionsAsync() => _service.GetDefinitionsAsync();
    [HttpPost]
    [Authorize(SubscriptionAdminPermissions.Products.Create)]
    public Task<AdminProductDto> CreateAsync([FromBody] CreateProductDto input) => _service.CreateAsync(input);
    [HttpPut("{id:guid}")]
    [Authorize(SubscriptionAdminPermissions.Products.Update)]
    public Task<AdminProductDto> UpdateAsync(Guid id, [FromBody] UpdateProductDto input) => _service.UpdateAsync(id, input);
    [HttpPut("{id:guid}/state")]
    [Authorize(SubscriptionAdminPermissions.Products.Publish)]
    public Task<AdminProductDto> SetStateAsync(Guid id, [FromBody] CatalogStateInputDto input) => _service.SetStateAsync(id, input);
    [HttpDelete("{id:guid}")]
    [Authorize(SubscriptionAdminPermissions.Products.Delete)]
    public Task DeleteAsync(Guid id, [FromQuery] VersionInputDto input) => _service.DeleteAsync(id, input);
}
