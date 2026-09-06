using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.Application.Services;

namespace SayHello.Subscription.Public.Entitlements;

public class EffectiveSubscriptionDto
{
    public bool HasEffectiveSubscription { get; set; }
    public UserSubscriptionDto? Subscription { get; set; }
}

public interface ICurrentUserEntitlementAppService : IApplicationService
{
    Task<EffectiveSubscriptionDto> GetAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode);

    Task<BooleanEntitlementResultDto> GetBooleanAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode,
        [Required, StringLength(SubscriptionConsts.MaxFeatureKeyLength)] string featureKey);

    Task<NumericEntitlementResultDto> GetNumericAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode,
        [Required, StringLength(SubscriptionConsts.MaxFeatureKeyLength)] string featureKey);
}
