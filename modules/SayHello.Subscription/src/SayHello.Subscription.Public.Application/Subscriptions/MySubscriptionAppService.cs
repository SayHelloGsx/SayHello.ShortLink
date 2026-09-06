using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Users;

namespace SayHello.Subscription.Public.Subscriptions;

[Authorize]
[RemoteService(IsEnabled = false)]
public class MySubscriptionAppService : SubscriptionApplicationService, IMySubscriptionAppService
{
    private readonly IUserSubscriptionRepository _subscriptions;

    public MySubscriptionAppService(IUserSubscriptionRepository subscriptions)
    {
        _subscriptions = subscriptions;
    }

    public virtual async Task<PagedResultDto<UserSubscriptionDto>> GetListAsync(GetMySubscriptionsInput input)
    {
        var owner = CurrentUser.GetId();
        var now = Clock.Now.ToUniversalTime();
        var page = await _subscriptions.GetPageAsync(new UserSubscriptionQuery(CurrentTenant.Id, now,
            UserId: owner, ProductId: input.ProductId, Status: input.Status, CurrentOnly: input.CurrentOnly,
            Filter: input.Filter, Sorting: input.Sorting, SkipCount: input.SkipCount,
            MaxResultCount: input.MaxResultCount), CancellationTokenProvider.Token);
        return SubscriptionDtoMapper.ToPage(page, subscription =>
        {
            EnsureOwner(subscription, owner);
            return SubscriptionDtoMapper.ToDto(subscription, now);
        });
    }

    public virtual async Task<UserSubscriptionDto> GetAsync(Guid id)
    {
        var owner = CurrentUser.GetId();
        var subscription = await _subscriptions.GetAsync(CurrentTenant.Id, id, CancellationTokenProvider.Token);
        EnsureOwner(subscription, owner);
        return SubscriptionDtoMapper.ToDto(subscription, Clock.Now.ToUniversalTime());
    }

    private void EnsureOwner(UserSubscription subscription, Guid owner)
    {
        if (subscription.UserId != owner || subscription.TenantId != CurrentTenant.Id)
        {
            throw new EntityNotFoundException(typeof(UserSubscription), subscription.Id);
        }
    }
}
