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
[Route("api/subscription/admin/bundles")]
[Authorize(SubscriptionAdminPermissions.Bundles.Default)]
public class BundleAdminController : AbpControllerBase, IBundleAdminAppService
{
    private readonly IBundleAdminAppService _service;
    public BundleAdminController(IBundleAdminAppService service) => _service = service;
    [HttpGet]
    public Task<PagedResultDto<AdminBundleDto>> GetListAsync([FromQuery] AdminCatalogQueryDto input) => _service.GetListAsync(input);
    [HttpGet("{id:guid}")]
    public Task<AdminBundleDto> GetAsync(Guid id) => _service.GetAsync(id);
    [HttpGet("plans")]
    public Task<PagedResultDto<AdminPlanDto>> GetPlansAsync([FromQuery] AdminCatalogQueryDto input) => _service.GetPlansAsync(input);
    [HttpPost]
    [Authorize(SubscriptionAdminPermissions.Bundles.Create)]
    public Task<AdminBundleDto> CreateAsync([FromBody] CreateBundleDto input) => _service.CreateAsync(input);
    [HttpPut("{id:guid}")]
    [Authorize(SubscriptionAdminPermissions.Bundles.Update)]
    public Task<AdminBundleDto> UpdateAsync(Guid id, [FromBody] UpdateBundleDto input) => _service.UpdateAsync(id, input);
    [HttpPut("{id:guid}/state")]
    [Authorize(SubscriptionAdminPermissions.Bundles.Publish)]
    public Task<AdminBundleDto> SetStateAsync(Guid id, [FromBody] CatalogStateInputDto input) => _service.SetStateAsync(id, input);
    [HttpDelete("{id:guid}")]
    [Authorize(SubscriptionAdminPermissions.Bundles.Delete)]
    public Task DeleteAsync(Guid id, [FromQuery] VersionInputDto input) => _service.DeleteAsync(id, input);
}
