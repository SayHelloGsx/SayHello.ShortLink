using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.Subscription.Admin.Users;

[RemoteService(Name = SubscriptionAdminRemoteServiceConsts.RemoteServiceName)]
[Area(SubscriptionAdminRemoteServiceConsts.ModuleName)]
[Route("api/subscription/admin/users")]
[Authorize(SubscriptionAdminPermissions.Users.Default)]
public class UserSubscriptionAdminController : AbpControllerBase, IUserSubscriptionAdminAppService
{
    private readonly IUserSubscriptionAdminAppService _service;
    public UserSubscriptionAdminController(IUserSubscriptionAdminAppService service) => _service = service;
    [HttpGet("lookup")]
    [Authorize(SubscriptionAdminPermissions.Users.Lookup)]
    public Task<PagedResultDto<SubscriptionUserDto>> LookupUsersAsync([FromQuery] UserLookupInputDto input) => _service.LookupUsersAsync(input);
    [HttpGet("subscriptions")]
    public Task<PagedResultDto<AdminUserSubscriptionDto>> GetListAsync([FromQuery] AdminSubscriptionQueryDto input) => _service.GetListAsync(input);
    [HttpGet("subscriptions/{id:guid}")]
    public Task<AdminUserSubscriptionDto> GetAsync(Guid id) => _service.GetAsync(id);
    [HttpGet("plans")]
    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public Task<PagedResultDto<AdminPlanDto>> GetPlansAsync([FromQuery] AdminCatalogQueryDto input) => _service.GetPlansAsync(input);
    [HttpGet("bundles")]
    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public Task<PagedResultDto<AdminBundleDto>> GetBundlesAsync([FromQuery] AdminCatalogQueryDto input) => _service.GetBundlesAsync(input);
    [HttpGet("{userId:guid}/preview/plan/{planId:guid}")]
    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public Task<AssignmentPreviewDto> PreviewPlanAsync(Guid userId, Guid planId) => _service.PreviewPlanAsync(userId, planId);
    [HttpGet("{userId:guid}/preview/bundle/{bundleId:guid}")]
    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public Task<AssignmentPreviewDto> PreviewBundleAsync(Guid userId, Guid bundleId) => _service.PreviewBundleAsync(userId, bundleId);
    [HttpPost("assign-plan")]
    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public Task<AdminUserSubscriptionDto> AssignPlanAsync([FromBody] AssignPlanDto input) => _service.AssignPlanAsync(input);
    [HttpPost("assign-bundle")]
    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public Task<ListResultDto<AdminUserSubscriptionDto>> AssignBundleAsync([FromBody] AssignBundleDto input) => _service.AssignBundleAsync(input);
    [HttpPost("subscriptions/{id:guid}/revoke")]
    [Authorize(SubscriptionAdminPermissions.Users.Revoke)]
    public Task<AdminUserSubscriptionDto> RevokeAsync(Guid id, [FromBody] RevokeSubscriptionDto input) => _service.RevokeAsync(id, input);
    [HttpPut("subscriptions/{id:guid}/expiration")]
    [Authorize(SubscriptionAdminPermissions.Users.AdjustExpiration)]
    public Task<AdminUserSubscriptionDto> AdjustExpirationAsync(Guid id, [FromBody] AdjustExpirationDto input) => _service.AdjustExpirationAsync(id, input);
}
