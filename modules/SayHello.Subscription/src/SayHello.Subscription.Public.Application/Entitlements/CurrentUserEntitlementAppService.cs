using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.Subscription.Entitlements;
using Volo.Abp;
using Volo.Abp.Users;

namespace SayHello.Subscription.Public.Entitlements;

[Authorize]
[RemoteService(IsEnabled = false)]
public class CurrentUserEntitlementAppService : SubscriptionApplicationService, ICurrentUserEntitlementAppService
{
    private readonly ISubscriptionEntitlementChecker _checker;

    public CurrentUserEntitlementAppService(ISubscriptionEntitlementChecker checker)
    {
        _checker = checker;
    }

    public virtual async Task<EffectiveSubscriptionDto> GetAsync(string productCode)
    {
        var subscription = await _checker.FindEffectiveSubscriptionAsync(CurrentTenant.Id, CurrentUser.GetId(),
            productCode, CancellationTokenProvider.Token);
        var now = Clock.Now.ToUniversalTime();
        if (subscription != null && !subscription.IsEffectiveAt(now))
        {
            subscription = null;
        }
        return new EffectiveSubscriptionDto
        {
            HasEffectiveSubscription = subscription != null,
            Subscription = subscription == null ? null : SubscriptionDtoMapper.ToDto(subscription, now)
        };
    }

    public virtual async Task<BooleanEntitlementResultDto> GetBooleanAsync(string productCode, string featureKey) =>
        SubscriptionDtoMapper.ToDto(await _checker.GetBooleanAsync(CurrentTenant.Id, CurrentUser.GetId(),
            productCode, featureKey, CancellationTokenProvider.Token));

    public virtual async Task<NumericEntitlementResultDto> GetNumericAsync(string productCode, string featureKey) =>
        SubscriptionDtoMapper.ToDto(await _checker.GetNumericAsync(CurrentTenant.Id, CurrentUser.GetId(),
            productCode, featureKey, CancellationTokenProvider.Token));
}
