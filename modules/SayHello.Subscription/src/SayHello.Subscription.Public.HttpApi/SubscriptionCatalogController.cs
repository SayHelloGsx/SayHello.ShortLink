using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Public.Catalog;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.Subscription.Public;

[AllowAnonymous]
[RemoteService(Name = SubscriptionPublicRemoteServiceConsts.RemoteServiceName)]
[Area(SubscriptionPublicRemoteServiceConsts.ModuleName)]
[Route("api/subscription/public")]
public class SubscriptionCatalogController : AbpControllerBase, ISubscriptionCatalogAppService
{
    private readonly ISubscriptionCatalogAppService _service;

    public SubscriptionCatalogController(ISubscriptionCatalogAppService service)
    {
        _service = service;
    }

    [HttpGet("products")]
    public Task<PagedResultDto<SubscriptionProductDto>> GetProductsAsync([FromQuery] GetPublicCatalogInput input) =>
        _service.GetProductsAsync(input);

    [HttpGet("products/{id:guid}")]
    public Task<SubscriptionProductDto> GetProductAsync(Guid id) => _service.GetProductAsync(id);

    [HttpGet("plans")]
    public Task<PagedResultDto<PublicSubscriptionPlanDto>> GetPlansAsync([FromQuery] GetPublicCatalogInput input) =>
        _service.GetPlansAsync(input);

    [HttpGet("plans/{id:guid}")]
    public Task<PublicSubscriptionPlanDto> GetPlanAsync(Guid id) => _service.GetPlanAsync(id);

    [HttpGet("bundles")]
    public Task<PagedResultDto<PublicSubscriptionBundleDto>> GetBundlesAsync([FromQuery] GetPublicCatalogInput input) =>
        _service.GetBundlesAsync(input);

    [HttpGet("bundles/{id:guid}")]
    public Task<PublicSubscriptionBundleDto> GetBundleAsync(Guid id) => _service.GetBundleAsync(id);
}
