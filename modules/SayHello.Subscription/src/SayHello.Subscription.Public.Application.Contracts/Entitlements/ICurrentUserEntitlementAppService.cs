using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Public.Catalog;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.Subscription.Public.Entitlements;

public class EffectiveSubscriptionDto
{
    public bool HasEffectiveSubscription { get; set; }
    public UserSubscriptionDto? Subscription { get; set; }
    public EntitlementSource Source { get; set; }
    public Guid? PlanId { get; set; }
    public string? PlanCode { get; set; }
    public string? PlanName { get; set; }
    public bool EntitlementsAreLive { get; set; }
    public List<EntitlementDto> Entitlements { get; set; } = new();
}

public class DefaultSubscriptionPlanDto : SubscriptionPlanDto
{
    public EntitlementSource Source => EntitlementSource.DefaultPlan;
}

public interface ICurrentUserEntitlementAppService : IApplicationService
{
    Task<EffectiveSubscriptionDto> GetAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode);

    Task<PagedResultDto<DefaultSubscriptionPlanDto>> GetDefaultPlansAsync(GetPublicCatalogInput input);

    Task<BooleanEntitlementResultDto> GetBooleanAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode,
        [Required, StringLength(SubscriptionConsts.MaxFeatureKeyLength)] string featureKey);

    Task<NumericEntitlementResultDto> GetNumericAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode,
        [Required, StringLength(SubscriptionConsts.MaxFeatureKeyLength)] string featureKey);
}
