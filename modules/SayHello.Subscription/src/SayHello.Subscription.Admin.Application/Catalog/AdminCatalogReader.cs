using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;

namespace SayHello.Subscription.Admin.Catalog;

public class AdminCatalogReader : ITransientDependency
{
    private readonly ISubscriptionProductRepository _products;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly ISubscriptionBundleRepository _bundles;
    private readonly ISubscriptionDefinitionRegistry _definitions;
    private readonly IStringLocalizerFactory _localizers;
    private readonly ICurrentTenant _tenant;
    private readonly ICancellationTokenProvider _cancellation;

    public AdminCatalogReader(ISubscriptionProductRepository products, ISubscriptionPlanRepository plans,
        ISubscriptionBundleRepository bundles, ISubscriptionDefinitionRegistry definitions,
        IStringLocalizerFactory localizers, ICurrentTenant tenant, ICancellationTokenProvider cancellation)
    {
        _products = products; _plans = plans; _bundles = bundles; _definitions = definitions;
        _localizers = localizers; _tenant = tenant; _cancellation = cancellation;
    }

    public SubscriptionCatalogQuery Query(AdminCatalogQueryDto input, bool publishedOnly = false) =>
        new(_tenant.Id, input.Filter, input.State, publishedOnly, input.ProductId,
            input.Sorting, input.SkipCount, input.MaxResultCount);

    public async Task<PagedResultDto<AdminProductDto>> ProductsAsync(AdminCatalogQueryDto input) =>
        SubscriptionDtoMapper.ToPage(await _products.GetPageAsync(Query(input), _cancellation.Token), AdminDtoMapper.ToDto);

    public async Task<SubscriptionProduct> ProductAsync(Guid id) =>
        (await _products.GetByIdsAsync(_tenant.Id, new[] { id }, _cancellation.Token)).SingleOrDefault()
        ?? throw new EntityNotFoundException(typeof(SubscriptionProduct), id);

    public async Task<SubscriptionPlan> PlanAsync(Guid id) =>
        (await _plans.GetByIdsAsync(_tenant.Id, new[] { id }, _cancellation.Token)).SingleOrDefault()
        ?? throw new EntityNotFoundException(typeof(SubscriptionPlan), id);

    public async Task<SubscriptionBundle> BundleAsync(Guid id) =>
        (await _bundles.GetByIdsAsync(_tenant.Id, new[] { id }, _cancellation.Token)).SingleOrDefault()
        ?? throw new EntityNotFoundException(typeof(SubscriptionBundle), id);

    public RegisteredProductDto Definition(string code)
    {
        var definition = _definitions.GetProduct(code);
        return new RegisteredProductDto
        {
            Code = definition.Code, DisplayName = definition.DisplayName.Localize(_localizers).Value,
            Features = definition.Features.Values.Select(feature => new RegisteredFeatureDto
            {
                Key = feature.Key, DisplayName = feature.DisplayName.Localize(_localizers).Value,
                Description = feature.Description?.Localize(_localizers).Value, Type = feature.Type,
                Maximum = feature.Maximum, AllowUnlimited = feature.AllowUnlimited
            }).ToList()
        };
    }

    public ListResultDto<RegisteredProductDto> Definitions() =>
        new(_definitions.GetProducts().Select(product => Definition(product.Code)).ToList());

    public async Task<AdminPlanDto> MapAsync(SubscriptionPlan plan) =>
        (await MapPlansAsync(new[] { plan })).Single();

    private async Task<List<AdminPlanDto>> MapPlansAsync(IReadOnlyCollection<SubscriptionPlan> plans)
    {
        var products = (await _products.GetByIdsAsync(_tenant.Id,
            plans.Select(plan => plan.ProductId).Distinct().ToArray(), _cancellation.Token)).ToDictionary(p => p.Id);
        return plans.Select(plan =>
        {
            var product = products[plan.ProductId];
            return AdminDtoMapper.ToDto(plan, SubscriptionDtoMapper.ToDto(plan, product,
                key => _definitions.GetFeature(product.Code, key).DisplayName.Localize(_localizers).Value));
        }).ToList();
    }

    public async Task<PagedResultDto<AdminPlanDto>> PlansAsync(AdminCatalogQueryDto input, bool publishedOnly = false)
    {
        var page = await _plans.GetPageAsync(Query(input, publishedOnly), _cancellation.Token);
        return new PagedResultDto<AdminPlanDto>(page.TotalCount, await MapPlansAsync(page.Items.ToArray()));
    }

    public async Task<AdminBundleDto> MapAsync(SubscriptionBundle bundle) =>
        (await MapBundlesAsync(new[] { bundle })).Single();

    private async Task<List<AdminBundleDto>> MapBundlesAsync(IReadOnlyCollection<SubscriptionBundle> bundles)
    {
        var plans = await _plans.GetByIdsAsync(_tenant.Id,
            bundles.SelectMany(bundle => bundle.Items).Select(item => item.PlanId).Distinct().ToArray(), _cancellation.Token);
        var mapped = (await MapPlansAsync(plans.ToArray())).ToDictionary(p => p.Id, p => (SubscriptionPlanDto)p);
        return bundles.Select(bundle => AdminDtoMapper.ToDto(bundle, SubscriptionDtoMapper.ToDto(bundle, mapped))).ToList();
    }

    public async Task<PagedResultDto<AdminBundleDto>> BundlesAsync(AdminCatalogQueryDto input, bool publishedOnly = false)
    {
        var page = await _bundles.GetPageAsync(Query(input, publishedOnly), _cancellation.Token);
        return new PagedResultDto<AdminBundleDto>(page.TotalCount, await MapBundlesAsync(page.Items.ToArray()));
    }
}
