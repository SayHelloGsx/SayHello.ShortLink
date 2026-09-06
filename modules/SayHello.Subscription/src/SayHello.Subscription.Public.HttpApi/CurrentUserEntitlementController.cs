using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Public.Entitlements;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.Subscription.Public;

[Authorize]
[RemoteService(Name = SubscriptionPublicRemoteServiceConsts.RemoteServiceName)]
[Area(SubscriptionPublicRemoteServiceConsts.ModuleName)]
[Route("api/subscription/public/entitlements/{productCode}")]
public class CurrentUserEntitlementController : AbpControllerBase, ICurrentUserEntitlementAppService
{
    private readonly ICurrentUserEntitlementAppService _service;

    public CurrentUserEntitlementController(ICurrentUserEntitlementAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<EffectiveSubscriptionDto> GetAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode) =>
        _service.GetAsync(productCode);

    [HttpGet("boolean/{featureKey}")]
    public Task<BooleanEntitlementResultDto> GetBooleanAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode,
        [Required, StringLength(SubscriptionConsts.MaxFeatureKeyLength)] string featureKey) =>
        _service.GetBooleanAsync(productCode, featureKey);

    [HttpGet("numeric/{featureKey}")]
    public Task<NumericEntitlementResultDto> GetNumericAsync(
        [Required, StringLength(SubscriptionConsts.MaxCodeLength)] string productCode,
        [Required, StringLength(SubscriptionConsts.MaxFeatureKeyLength)] string featureKey) =>
        _service.GetNumericAsync(productCode, featureKey);
}
