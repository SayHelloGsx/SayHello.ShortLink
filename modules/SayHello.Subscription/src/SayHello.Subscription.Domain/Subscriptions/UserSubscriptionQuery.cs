using System;
using Volo.Abp;

namespace SayHello.Subscription.Subscriptions;

public sealed record UserSubscriptionQuery(
    Guid? TenantId,
    DateTime Now,
    Guid? UserId = null,
    Guid? ProductId = null,
    UserSubscriptionStatus? Status = null,
    bool CurrentOnly = false,
    string? Filter = null,
    UserSubscriptionSort Sorting = UserSubscriptionSort.StartsAtDescending,
    int SkipCount = 0,
    int MaxResultCount = 20)
{
    public void Validate()
    {
        SubscriptionGuard.Utc(Now);
        SubscriptionGuard.Paging(SkipCount, MaxResultCount);
        if (!Enum.IsDefined(Sorting) || (Status.HasValue && !Enum.IsDefined(Status.Value)) ||
            (Filter?.Length ?? 0) > SubscriptionConsts.MaxNameLength)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidPaging);
        }
    }
}
