using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Public.Catalog;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Users;

namespace SayHello.Subscription.Public.Entitlements;

[Authorize]
[RemoteService(IsEnabled = false)]
public class CurrentUserEntitlementAppService : SubscriptionApplicationService, ICurrentUserEntitlementAppService
{
    private readonly ISubscriptionEntitlementChecker _checker;
    private readonly ISubscriptionDefinitionRegistry _definitions;
    private readonly IStringLocalizerFactory _localizers;

    public CurrentUserEntitlementAppService(ISubscriptionEntitlementChecker checker,
        ISubscriptionDefinitionRegistry definitions, IStringLocalizerFactory localizers)
    {
        _checker = checker;
        _definitions = definitions;
        _localizers = localizers;
    }

    public virtual async Task<EffectiveSubscriptionDto> GetAsync(string productCode)
    {
        var userId = CurrentUser.GetId();
        var context = await _checker.ResolveAsync(CurrentTenant.Id, userId,
            productCode, CancellationTokenProvider.Token);
        var now = Clock.Now.ToUniversalTime();
        if (context.Subscription != null && !context.Subscription.IsEffectiveAt(now))
        {
            context = await _checker.ResolveAsync(CurrentTenant.Id, userId,
                productCode, CancellationTokenProvider.Token);
            now = Clock.Now.ToUniversalTime();
        }

        if (context.Source == EntitlementSource.Subscription &&
            context.Subscription is { } subscription && subscription.IsEffectiveAt(now))
        {
            var snapshot = SubscriptionDtoMapper.ToDto(subscription, now);
            return new EffectiveSubscriptionDto
            {
                HasEffectiveSubscription = true,
                Subscription = snapshot,
                Source = EntitlementSource.Subscription,
                PlanId = context.PlanId,
                PlanCode = snapshot.PlanCode,
                PlanName = snapshot.PlanName,
                Entitlements = snapshot.Entitlements.ToList()
            };
        }

        if (context.Source == EntitlementSource.DefaultPlan && context.DefaultPlan is { } defaultPlan)
        {
            var plan = MapDefaultPlan(defaultPlan);
            return new EffectiveSubscriptionDto
            {
                Source = EntitlementSource.DefaultPlan,
                PlanId = plan.Id,
                PlanCode = plan.Code,
                PlanName = plan.Name,
                EntitlementsAreLive = true,
                Entitlements = plan.Entitlements
            };
        }

        return new EffectiveSubscriptionDto();
    }

    public virtual async Task<PagedResultDto<DefaultSubscriptionPlanDto>> GetDefaultPlansAsync(GetPublicCatalogInput input)
    {
        var query = new SubscriptionCatalogQuery(CurrentTenant.Id, input.Filter, PublishedOnly: true,
            ProductId: input.ProductId, Sorting: input.Sorting, SkipCount: input.SkipCount,
            MaxResultCount: input.MaxResultCount);
        var page = await _checker.GetDefaultPlansAsync(CurrentTenant.Id, CurrentUser.GetId(), query,
            CancellationTokenProvider.Token);
        return SubscriptionDtoMapper.ToPage(page, MapDefaultPlan);
    }

    public virtual async Task<BooleanEntitlementResultDto> GetBooleanAsync(string productCode, string featureKey) =>
        SubscriptionDtoMapper.ToDto(await _checker.GetBooleanAsync(CurrentTenant.Id, CurrentUser.GetId(),
            productCode, featureKey, CancellationTokenProvider.Token));

    public virtual async Task<NumericEntitlementResultDto> GetNumericAsync(string productCode, string featureKey) =>
        SubscriptionDtoMapper.ToDto(await _checker.GetNumericAsync(CurrentTenant.Id, CurrentUser.GetId(),
            productCode, featureKey, CancellationTokenProvider.Token));

    private DefaultSubscriptionPlanDto MapDefaultPlan(DefaultSubscriptionPlan defaultPlan)
    {
        var (product, plan) = defaultPlan;
        if (product.TenantId != CurrentTenant.Id || plan.TenantId != CurrentTenant.Id ||
            product.State != SubscriptionCatalogState.Published || plan.State != SubscriptionCatalogState.Published ||
            product.DefaultPlanId != plan.Id || plan.ProductId != product.Id)
        {
            throw new EntityNotFoundException(typeof(SubscriptionPlan), plan.Id);
        }

        var dto = SubscriptionDtoMapper.ToDto(plan, product,
            key => _definitions.GetFeature(product.Code, key).DisplayName.Localize(_localizers).Value);
        foreach (var entitlement in dto.Entitlements)
        {
            entitlement.Description = _definitions.GetFeature(product.Code, entitlement.FeatureKey)
                .Description?.Localize(_localizers).Value;
        }

        return new DefaultSubscriptionPlanDto
        {
            Id = dto.Id,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            DisplayOrder = dto.DisplayOrder,
            ProductId = dto.ProductId,
            ProductCode = dto.ProductCode,
            ProductName = dto.ProductName,
            Entitlements = dto.Entitlements
        };
    }
}
