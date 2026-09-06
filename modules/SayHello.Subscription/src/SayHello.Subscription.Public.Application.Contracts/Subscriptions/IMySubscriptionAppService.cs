using System;
using System.Threading.Tasks;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.Subscription.Public.Subscriptions;

public interface IMySubscriptionAppService : IApplicationService
{
    Task<PagedResultDto<UserSubscriptionDto>> GetListAsync(GetMySubscriptionsInput input);
    Task<UserSubscriptionDto> GetAsync(Guid id);
}
