using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;
using SayHello.Subscription.Subscriptions;
using SayHello.Subscription.Users;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace SayHello.Subscription.Admin.Users;

[RemoteService(false)]
[Authorize(SubscriptionAdminPermissions.Users.Default)]
public class UserSubscriptionAdminAppService : SubscriptionApplicationService, IUserSubscriptionAdminAppService
{
    private readonly ISubscriptionManager _manager;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly ISubscriptionUserDirectory _users;
    private readonly AdminCatalogReader _catalog;
    private readonly IClock _clock;

    public UserSubscriptionAdminAppService(ISubscriptionManager manager, IUserSubscriptionRepository subscriptions,
        ISubscriptionUserDirectory users, AdminCatalogReader catalog, IClock clock)
    {
        _manager = manager; _subscriptions = subscriptions; _users = users; _catalog = catalog; _clock = clock;
    }

    [Authorize(SubscriptionAdminPermissions.Users.Lookup)]
    public virtual async Task<PagedResultDto<SubscriptionUserDto>> LookupUsersAsync(UserLookupInputDto input) =>
        SubscriptionDtoMapper.ToPage(await _users.SearchAsync(CurrentTenant.Id, input.Filter, input.SkipCount,
            input.MaxResultCount, CancellationTokenProvider.Token), user => new SubscriptionUserDto
        {
            Id = user.Id, UserName = user.UserName, DisplayName = $"{user.Name} {user.Surname}".Trim(),
            Email = user.Email, IsActive = user.IsActive
        });

    public virtual async Task<PagedResultDto<AdminUserSubscriptionDto>> GetListAsync(AdminSubscriptionQueryDto input)
    {
        var now = _clock.Now.ToUniversalTime();
        var page = await _subscriptions.GetPageAsync(new UserSubscriptionQuery(CurrentTenant.Id, now, input.UserId,
            input.ProductId, input.Status, input.CurrentOnly, input.Filter, input.Sorting,
            input.SkipCount, input.MaxResultCount), CancellationTokenProvider.Token);
        return SubscriptionDtoMapper.ToPage(page, subscription => AdminDtoMapper.ToDto(subscription, now));
    }

    public virtual async Task<AdminUserSubscriptionDto> GetAsync(Guid id) =>
        AdminDtoMapper.ToDto(await _subscriptions.GetAsync(CurrentTenant.Id, id, CancellationTokenProvider.Token), _clock.Now.ToUniversalTime());

    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public virtual Task<PagedResultDto<AdminPlanDto>> GetPlansAsync(AdminCatalogQueryDto input) =>
        _catalog.PlansAsync(input, publishedOnly: true);

    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public virtual Task<PagedResultDto<AdminBundleDto>> GetBundlesAsync(AdminCatalogQueryDto input) =>
        _catalog.BundlesAsync(input, publishedOnly: true);

    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public virtual async Task<AssignmentPreviewDto> PreviewPlanAsync(Guid userId, Guid planId) =>
        AdminDtoMapper.ToDto(await _manager.PreviewPlanAsync(CurrentTenant.Id, userId, planId, CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    public virtual async Task<AssignmentPreviewDto> PreviewBundleAsync(Guid userId, Guid bundleId) =>
        AdminDtoMapper.ToDto(await _manager.PreviewBundleAsync(CurrentTenant.Id, userId, bundleId, CancellationTokenProvider.Token));

    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminUserSubscriptionDto> AssignPlanAsync(AssignPlanDto input) =>
        AdminDtoMapper.ToDto(await _manager.AssignPlanAsync(new AssignSubscriptionPlan(CurrentTenant.Id, input.UserId,
            AdminDtoMapper.ToTarget(input.Target)), CancellationTokenProvider.Token), _clock.Now.ToUniversalTime());

    [Authorize(SubscriptionAdminPermissions.Users.Assign)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<ListResultDto<AdminUserSubscriptionDto>> AssignBundleAsync(AssignBundleDto input)
    {
        // One manager command owns the transaction; never loop over AssignPlanAsync here.
        var subscriptions = await _manager.AssignBundleAsync(new AssignSubscriptionBundle(CurrentTenant.Id, input.UserId,
            input.BundleId, input.BundleConcurrencyStamp, input.Targets.Select(AdminDtoMapper.ToTarget)),
            CancellationTokenProvider.Token);
        var now = _clock.Now.ToUniversalTime();
        return new ListResultDto<AdminUserSubscriptionDto>(subscriptions.Select(s => AdminDtoMapper.ToDto(s, now)).ToList());
    }

    [Authorize(SubscriptionAdminPermissions.Users.Revoke)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminUserSubscriptionDto> RevokeAsync(Guid id, RevokeSubscriptionDto input) =>
        AdminDtoMapper.ToDto(await _manager.RevokeAsync(CurrentTenant.Id, id, input.ConcurrencyStamp, input.Reason,
            CancellationTokenProvider.Token), _clock.Now.ToUniversalTime());

    [Authorize(SubscriptionAdminPermissions.Users.AdjustExpiration)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<AdminUserSubscriptionDto> AdjustExpirationAsync(Guid id, AdjustExpirationDto input) =>
        AdminDtoMapper.ToDto(await _manager.AdjustExpirationAsync(CurrentTenant.Id, id, input.ConcurrencyStamp,
            input.ExpiresAt, CancellationTokenProvider.Token), _clock.Now.ToUniversalTime());
}
