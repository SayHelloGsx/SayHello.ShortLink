using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Threading;

namespace SayHello.Subscription.Admin.Catalog;

public class AdminCatalogReader : ITransientDependency
{
    private readonly ISubscriptionProductRepository _products;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly ISubscriptionBundleRepository _bundles;
    private readonly ISubscriptionDefinitionRegistry _definitions;
    private readonly IEnumerable<ISubscriptionEntitlementOptionProvider> _optionProviders;
    private readonly IStringLocalizerFactory _localizers;
    private readonly ICancellationTokenProvider _cancellation;

    public AdminCatalogReader(ISubscriptionProductRepository products, ISubscriptionPlanRepository plans,
        ISubscriptionBundleRepository bundles, ISubscriptionDefinitionRegistry definitions,
        IEnumerable<ISubscriptionEntitlementOptionProvider> optionProviders,
        IStringLocalizerFactory localizers, ICancellationTokenProvider cancellation)
    {
        _products = products; _plans = plans; _bundles = bundles; _definitions = definitions;
        _optionProviders = optionProviders;
        _localizers = localizers; _cancellation = cancellation;
    }

    public SubscriptionCatalogQuery Query(AdminCatalogQueryDto input, bool publishedOnly = false) =>
        new(input.Filter, input.State, publishedOnly, input.ProductId,
            input.Sorting, input.SkipCount, input.MaxResultCount);

    public async Task<PagedResultDto<AdminProductDto>> ProductsAsync(AdminCatalogQueryDto input)
    {
        var page = await _products.GetPageAsync(Query(input), _cancellation.Token);
        return new PagedResultDto<AdminProductDto>(page.TotalCount, await MapProductsAsync(page.Items.ToArray()));
    }

    public async Task<AdminProductDto> MapAsync(SubscriptionProduct product) =>
        (await MapProductsAsync(new[] { product })).Single();

    private async Task<List<AdminProductDto>> MapProductsAsync(IReadOnlyCollection<SubscriptionProduct> products)
    {
        var ids = products.Where(product => product.DefaultPlanId.HasValue)
            .Select(product => product.DefaultPlanId!.Value).Distinct().ToArray();
        var defaults = ids.Length == 0
            ? new Dictionary<Guid, SubscriptionPlan>()
            : (await _plans.GetByIdsAsync(ids, _cancellation.Token)).ToDictionary(plan => plan.Id);
        return products.Select(product => AdminDtoMapper.ToDto(product,
            product.DefaultPlanId.HasValue && defaults.TryGetValue(product.DefaultPlanId.Value, out var plan) &&
            plan.ProductId == product.Id ? plan.Name : null)).ToList();
    }

    public async Task<PagedResultDto<AdminPlanDto>> DefaultPlanOptionsAsync(Guid id, AdminCatalogQueryDto input)
    {
        var product = await ProductAsync(id);
        if (product.State != SubscriptionCatalogState.Published)
            return new PagedResultDto<AdminPlanDto>(0, Array.Empty<AdminPlanDto>());

        var query = Query(input, publishedOnly: true) with
        {
            ProductId = id, State = SubscriptionCatalogState.Published
        };
        var page = await _plans.GetPageAsync(query, _cancellation.Token);
        return new PagedResultDto<AdminPlanDto>(page.TotalCount, await MapPlansAsync(page.Items.ToArray()));
    }

    public async Task<SubscriptionProduct> ProductAsync(Guid id) =>
        (await _products.GetByIdsAsync(new[] { id }, _cancellation.Token)).SingleOrDefault()
        ?? throw new EntityNotFoundException(typeof(SubscriptionProduct), id);

    public async Task<SubscriptionPlan> PlanAsync(Guid id) =>
        (await _plans.GetByIdsAsync(new[] { id }, _cancellation.Token)).SingleOrDefault()
        ?? throw new EntityNotFoundException(typeof(SubscriptionPlan), id);

    public async Task<SubscriptionBundle> BundleAsync(Guid id) =>
        (await _bundles.GetByIdsAsync(new[] { id }, _cancellation.Token)).SingleOrDefault()
        ?? throw new EntityNotFoundException(typeof(SubscriptionBundle), id);

    public async Task<RegisteredProductDto> DefinitionAsync(string code)
    {
        var definition = _definitions.GetProduct(code);
        var features = new List<RegisteredFeatureDto>();
        foreach (var feature in definition.Features.Values)
        {
            var options = await _optionProviders.GetCanonicalOptionsAsync(
                definition.Code, feature, _cancellation.Token);
            features.Add(new RegisteredFeatureDto
            {
                Key = feature.Key,
                DisplayName = feature.DisplayName.Localize(_localizers).Value,
                Description = feature.Description?.Localize(_localizers).Value,
                Type = feature.Type,
                Maximum = feature.Maximum,
                AllowUnlimited = feature.AllowUnlimited,
                InputMode = InputMode(feature.Type, options.Count),
                Options = options.ToList()
            });
        }

        return new RegisteredProductDto
        {
            Code = definition.Code, DisplayName = definition.DisplayName.Localize(_localizers).Value,
            Features = features
        };
    }

    public async Task<ListResultDto<RegisteredProductDto>> DefinitionsAsync()
    {
        var result = new List<RegisteredProductDto>();
        foreach (var product in _definitions.GetProducts())
        {
            result.Add(await DefinitionAsync(product.Code));
        }

        return new ListResultDto<RegisteredProductDto>(result);
    }

    public async Task<AdminPlanDto> MapAsync(SubscriptionPlan plan) =>
        (await MapPlansAsync(new[] { plan })).Single();

    private async Task<List<AdminPlanDto>> MapPlansAsync(IReadOnlyCollection<SubscriptionPlan> plans)
    {
        var products = (await _products.GetByIdsAsync(
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
        var plans = await _plans.GetByIdsAsync(
            bundles.SelectMany(bundle => bundle.Items).Select(item => item.PlanId).Distinct().ToArray(), _cancellation.Token);
        var mapped = (await MapPlansAsync(plans.ToArray())).ToDictionary(p => p.Id, p => (SubscriptionPlanDto)p);
        return bundles.Select(bundle => AdminDtoMapper.ToDto(bundle, SubscriptionDtoMapper.ToDto(bundle, mapped))).ToList();
    }

    public async Task<PagedResultDto<AdminBundleDto>> BundlesAsync(AdminCatalogQueryDto input, bool publishedOnly = false)
    {
        var page = await _bundles.GetPageAsync(Query(input, publishedOnly), _cancellation.Token);
        return new PagedResultDto<AdminBundleDto>(page.TotalCount, await MapBundlesAsync(page.Items.ToArray()));
    }

    private static SubscriptionEntitlementInputMode InputMode(
        SubscriptionEntitlementType type, int optionCount) =>
        type switch
        {
            SubscriptionEntitlementType.Boolean => SubscriptionEntitlementInputMode.Boolean,
            SubscriptionEntitlementType.Numeric => SubscriptionEntitlementInputMode.Numeric,
            SubscriptionEntitlementType.Enum => SubscriptionEntitlementInputMode.Select,
            SubscriptionEntitlementType.StringSet when optionCount > 0 =>
                SubscriptionEntitlementInputMode.MultiSelect,
            SubscriptionEntitlementType.StringSet => SubscriptionEntitlementInputMode.MultiText,
            _ => throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue)
        };
}
