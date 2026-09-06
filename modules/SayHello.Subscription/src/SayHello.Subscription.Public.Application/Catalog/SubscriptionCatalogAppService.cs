using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace SayHello.Subscription.Public.Catalog;

[AllowAnonymous]
[RemoteService(IsEnabled = false)]
public class SubscriptionCatalogAppService : SubscriptionApplicationService, ISubscriptionCatalogAppService
{
    private readonly ISubscriptionProductRepository _products;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly ISubscriptionBundleRepository _bundles;
    private readonly ISubscriptionDefinitionRegistry _definitions;
    private readonly IStringLocalizerFactory _localizers;

    public SubscriptionCatalogAppService(ISubscriptionProductRepository products, ISubscriptionPlanRepository plans,
        ISubscriptionBundleRepository bundles, ISubscriptionDefinitionRegistry definitions,
        IStringLocalizerFactory localizers)
    {
        _products = products;
        _plans = plans;
        _bundles = bundles;
        _definitions = definitions;
        _localizers = localizers;
    }

    public virtual async Task<PagedResultDto<SubscriptionProductDto>> GetProductsAsync(GetPublicCatalogInput input)
    {
        var page = await _products.GetPageAsync(Query(input), CancellationTokenProvider.Token);
        return SubscriptionDtoMapper.ToPage(page, product =>
        {
            EnsurePublished(product);
            return SubscriptionDtoMapper.ToDto(product);
        });
    }

    public virtual async Task<SubscriptionProductDto> GetProductAsync(Guid id)
    {
        var product = (await _products.GetByIdsAsync(CurrentTenant.Id, new[] { id },
            CancellationTokenProvider.Token)).SingleOrDefault();
        EnsurePublished(product, id);
        return SubscriptionDtoMapper.ToDto(product!);
    }

    public virtual async Task<PagedResultDto<PublicSubscriptionPlanDto>> GetPlansAsync(GetPublicCatalogInput input)
    {
        var page = await _plans.GetPageAsync(Query(input), CancellationTokenProvider.Token);
        var mapped = await MapPlansAsync(page.Items);
        return new PagedResultDto<PublicSubscriptionPlanDto>(page.TotalCount, page.Items.Select(p => mapped[p.Id]).ToList());
    }

    public virtual async Task<PublicSubscriptionPlanDto> GetPlanAsync(Guid id)
    {
        var plan = (await _plans.GetByIdsAsync(CurrentTenant.Id, new[] { id },
            CancellationTokenProvider.Token)).SingleOrDefault();
        EnsurePublished(plan, id);
        return (await MapPlansAsync(new[] { plan! }))[id];
    }

    public virtual async Task<PagedResultDto<PublicSubscriptionBundleDto>> GetBundlesAsync(GetPublicCatalogInput input)
    {
        var page = await _bundles.GetPageAsync(Query(input), CancellationTokenProvider.Token);
        var mapped = await MapBundlesAsync(page.Items);
        return new PagedResultDto<PublicSubscriptionBundleDto>(page.TotalCount, mapped);
    }

    public virtual async Task<PublicSubscriptionBundleDto> GetBundleAsync(Guid id)
    {
        var bundle = (await _bundles.GetByIdsAsync(CurrentTenant.Id, new[] { id },
            CancellationTokenProvider.Token)).SingleOrDefault();
        EnsurePublished(bundle, id);
        return (await MapBundlesAsync(new[] { bundle! })).Single();
    }

    private SubscriptionCatalogQuery Query(GetPublicCatalogInput input) =>
        new(CurrentTenant.Id, input.Filter, PublishedOnly: true, ProductId: input.ProductId,
            Sorting: input.Sorting, SkipCount: input.SkipCount, MaxResultCount: input.MaxResultCount);

    private async Task<Dictionary<Guid, PublicSubscriptionPlanDto>> MapPlansAsync(
        IReadOnlyCollection<SubscriptionPlan> plans,
        IReadOnlyDictionary<Guid, SubscriptionProduct>? loadedProducts = null)
    {
        var products = loadedProducts ?? (await _products.GetByIdsAsync(CurrentTenant.Id,
            plans.Select(p => p.ProductId).Distinct().ToArray(), CancellationTokenProvider.Token)).ToDictionary(p => p.Id);
        var result = new Dictionary<Guid, PublicSubscriptionPlanDto>();
        foreach (var plan in plans)
        {
            EnsurePublished(plan);
            if (!products.TryGetValue(plan.ProductId, out var product) ||
                product.State != SubscriptionCatalogState.Published || product.TenantId != CurrentTenant.Id)
            {
                throw new EntityNotFoundException(typeof(SubscriptionPlan), plan.Id);
            }

            var dto = SubscriptionDtoMapper.ToDto(plan, product,
                key => _definitions.GetFeature(product.Code, key).DisplayName.Localize(_localizers).Value);
            result.Add(plan.Id, new PublicSubscriptionPlanDto
            {
                Id = dto.Id,
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                DisplayOrder = dto.DisplayOrder,
                ProductId = dto.ProductId,
                ProductCode = dto.ProductCode,
                ProductName = dto.ProductName,
                Entitlements = dto.Entitlements.Select(entitlement => new PublicEntitlementDto
                {
                    FeatureKey = entitlement.FeatureKey,
                    DisplayName = entitlement.DisplayName,
                    Description = _definitions.GetFeature(product.Code, entitlement.FeatureKey)
                        .Description?.Localize(_localizers).Value,
                    Value = entitlement.Value
                }).ToList()
            });
        }

        return result;
    }

    private async Task<List<PublicSubscriptionBundleDto>> MapBundlesAsync(IReadOnlyCollection<SubscriptionBundle> bundles)
    {
        var plans = await _plans.GetByIdsAsync(CurrentTenant.Id,
            bundles.SelectMany(b => b.Items).Select(i => i.PlanId).Distinct().ToArray(),
            CancellationTokenProvider.Token);
        var products = await _products.GetByIdsAsync(CurrentTenant.Id,
            plans.Select(p => p.ProductId).Distinct().ToArray(), CancellationTokenProvider.Token);
        foreach (var bundle in bundles)
        {
            EnsurePublished(bundle);
            if (bundle.Items.Count < 2 || bundle.Items.Any(item =>
                    !plans.Any(p => p.Id == item.PlanId && p.ProductId == item.ProductId &&
                        p.TenantId == CurrentTenant.Id && p.State == SubscriptionCatalogState.Published) ||
                    !products.Any(p => p.Id == item.ProductId && p.TenantId == CurrentTenant.Id &&
                        p.State == SubscriptionCatalogState.Published)))
            {
                throw new EntityNotFoundException(typeof(SubscriptionBundle), bundle.Id);
            }
        }

        var mappedPlans = await MapPlansAsync(plans, products.ToDictionary(p => p.Id));
        return bundles.Select(bundle => new PublicSubscriptionBundleDto
        {
            Id = bundle.Id,
            Code = bundle.Code,
            Name = bundle.Name,
            Description = bundle.Description,
            DisplayOrder = bundle.DisplayOrder,
            Items = bundle.Items.Select(item =>
            {
                var plan = mappedPlans[item.PlanId];
                return new PublicSubscriptionBundleItemDto
                {
                    ProductId = plan.ProductId,
                    ProductCode = plan.ProductCode,
                    ProductName = plan.ProductName,
                    PlanId = plan.Id,
                    PlanCode = plan.Code,
                    PlanName = plan.Name,
                    Entitlements = plan.Entitlements.ToList()
                };
            }).ToList()
        }).ToList();
    }

    private void EnsurePublished(SubscriptionProduct? product, Guid? id = null)
    {
        if (product == null || product.TenantId != CurrentTenant.Id || product.State != SubscriptionCatalogState.Published)
        {
            throw new EntityNotFoundException(typeof(SubscriptionProduct), id ?? product?.Id);
        }
    }

    private void EnsurePublished(SubscriptionPlan? plan, Guid? id = null)
    {
        if (plan == null || plan.TenantId != CurrentTenant.Id || plan.State != SubscriptionCatalogState.Published)
        {
            throw new EntityNotFoundException(typeof(SubscriptionPlan), id ?? plan?.Id);
        }
    }

    private void EnsurePublished(SubscriptionBundle? bundle, Guid? id = null)
    {
        if (bundle == null || bundle.TenantId != CurrentTenant.Id || bundle.State != SubscriptionCatalogState.Published)
        {
            throw new EntityNotFoundException(typeof(SubscriptionBundle), id ?? bundle?.Id);
        }
    }
}
