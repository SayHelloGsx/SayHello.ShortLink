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
[Route("api/subscription/admin/plans")]
[Authorize(SubscriptionAdminPermissions.Plans.Default)]
public class PlanAdminController : AbpControllerBase, IPlanAdminAppService
{
    private readonly IPlanAdminAppService _service;
    public PlanAdminController(IPlanAdminAppService service) => _service = service;
    [HttpGet]
    public virtual Task<PagedResultDto<AdminPlanDto>> GetListAsync([FromQuery] AdminCatalogQueryDto input) => _service.GetListAsync(input);
    [HttpGet("{id:guid}")]
    public virtual Task<AdminPlanDto> GetAsync(Guid id) => _service.GetAsync(id);
    [HttpGet("products")]
    public virtual Task<PagedResultDto<AdminProductDto>> GetProductsAsync([FromQuery] AdminCatalogQueryDto input) => _service.GetProductsAsync(input);
    [HttpGet("products/{productId:guid}/definition")]
    public virtual Task<RegisteredProductDto> GetDefinitionAsync(Guid productId) => _service.GetDefinitionAsync(productId);
    [HttpPost]
    [Authorize(SubscriptionAdminPermissions.Plans.Create)]
    public virtual Task<AdminPlanDto> CreateAsync([FromBody] CreatePlanDto input) => _service.CreateAsync(input);
    [HttpPut("{id:guid}")]
    [Authorize(SubscriptionAdminPermissions.Plans.Update)]
    public virtual Task<AdminPlanDto> UpdateAsync(Guid id, [FromBody] UpdatePlanDto input) => _service.UpdateAsync(id, input);
    [HttpPut("{id:guid}/state")]
    [Authorize(SubscriptionAdminPermissions.Plans.Publish)]
    public virtual Task<AdminPlanDto> SetStateAsync(Guid id, [FromBody] CatalogStateInputDto input) => _service.SetStateAsync(id, input);
    [HttpDelete("{id:guid}")]
    [Authorize(SubscriptionAdminPermissions.Plans.Delete)]
    public virtual Task DeleteAsync(Guid id, [FromQuery] VersionInputDto input) => _service.DeleteAsync(id, input);
}
