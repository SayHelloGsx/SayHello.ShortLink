using System;
using System.Threading.Tasks;
using SayHello.Subscription.Admin.Catalog;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.Subscription.Admin.Users;

public interface IUserSubscriptionAdminAppService : IApplicationService
{
    Task<PagedResultDto<SubscriptionUserDto>> LookupUsersAsync(UserLookupInputDto input);
    Task<PagedResultDto<AdminUserSubscriptionDto>> GetListAsync(AdminSubscriptionQueryDto input);
    Task<AdminUserSubscriptionDto> GetAsync(Guid id);
    Task<PagedResultDto<AdminPlanDto>> GetPlansAsync(AdminCatalogQueryDto input);
    Task<PagedResultDto<AdminBundleDto>> GetBundlesAsync(AdminCatalogQueryDto input);
    Task<AssignmentPreviewDto> PreviewPlanAsync(Guid userId, Guid planId);
    Task<AssignmentPreviewDto> PreviewBundleAsync(Guid userId, Guid bundleId);
    Task<AdminUserSubscriptionDto> AssignPlanAsync(AssignPlanDto input);
    Task<ListResultDto<AdminUserSubscriptionDto>> AssignBundleAsync(AssignBundleDto input);
    Task<AdminUserSubscriptionDto> RevokeAsync(Guid id, RevokeSubscriptionDto input);
    Task<AdminUserSubscriptionDto> AdjustExpirationAsync(Guid id, AdjustExpirationDto input);
}
