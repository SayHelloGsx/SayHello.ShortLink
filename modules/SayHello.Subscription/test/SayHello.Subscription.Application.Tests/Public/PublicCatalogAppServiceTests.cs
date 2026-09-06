using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using NSubstitute;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Public.Catalog;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace SayHello.Subscription.Public;

public class PublicCatalogAppServiceTests
{
    private readonly PublicServiceTestContext _context = new();
    private readonly ISubscriptionProductRepository _products = Substitute.For<ISubscriptionProductRepository>();
    private readonly ISubscriptionPlanRepository _plans = Substitute.For<ISubscriptionPlanRepository>();
    private readonly ISubscriptionBundleRepository _bundles = Substitute.For<ISubscriptionBundleRepository>();
    private readonly ISubscriptionDefinitionRegistry _definitions = Substitute.For<ISubscriptionDefinitionRegistry>();
    private readonly SubscriptionCatalogAppService _service;

    public PublicCatalogAppServiceTests()
    {
        _context.CurrentUser.Id.Returns((Guid?)null);
        _context.CurrentUser.IsAuthenticated.Returns(false);
        _service = _context.Configure(new SubscriptionCatalogAppService(_products, _plans, _bundles,
            _definitions, Substitute.For<IStringLocalizerFactory>()));
        _products.GetByIdsAsync(Arg.Any<Guid?>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SubscriptionProduct>());
        _plans.GetByIdsAsync(Arg.Any<Guid?>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SubscriptionPlan>());
        _bundles.GetByIdsAsync(Arg.Any<Guid?>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SubscriptionBundle>());
    }

    [Fact]
    public async Task Anonymous_catalog_lists_forward_bounded_published_filters_and_preserve_counts()
    {
        var (_, product, plan) = SeedPlan("one");
        var input = new GetPublicCatalogInput
        {
            Filter = "standard",
            ProductId = product.Id,
            SkipCount = 2,
            MaxResultCount = 3,
            Sorting = SubscriptionCatalogSort.NameDescending
        };
        _products.GetPageAsync(Arg.Any<SubscriptionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<SubscriptionProduct>(7, new[] { product }));
        _plans.GetPageAsync(Arg.Any<SubscriptionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<SubscriptionPlan>(7, new[] { plan }));
        _bundles.GetPageAsync(Arg.Any<SubscriptionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<SubscriptionBundle>(7, Array.Empty<SubscriptionBundle>()));

        (await _service.GetProductsAsync(input)).TotalCount.ShouldBe(7);
        var plans = await _service.GetPlansAsync(input);
        plans.TotalCount.ShouldBe(7);
        plans.Items.Single().ProductCode.ShouldBe(product.Code);
        (await _service.GetBundlesAsync(input)).TotalCount.ShouldBe(7);

        foreach (var calls in new[] { _products.ReceivedCalls(), _plans.ReceivedCalls(), _bundles.ReceivedCalls() })
        {
            var query = calls.Single(c => c.GetMethodInfo().Name == "GetPageAsync")
                .GetArguments()[0].ShouldBeOfType<SubscriptionCatalogQuery>();
            query.PublishedOnly.ShouldBeTrue();
            query.State.ShouldBeNull();
            query.TenantId.ShouldBe(_context.TenantId);
            query.ProductId.ShouldBe(product.Id);
            query.Filter.ShouldBe(input.Filter);
            query.Sorting.ShouldBe(input.Sorting);
            query.SkipCount.ShouldBe(2);
            query.MaxResultCount.ShouldBe(3);
        }
    }

    [Fact]
    public async Task Plan_details_include_typed_zero_and_localized_feature_descriptions()
    {
        var (_, _, plan) = SeedPlan("one");
        var result = await _service.GetPlanAsync(plan.Id);
        result.Entitlements.Single(e => e.FeatureKey == "enabled").Description.ShouldBe("Feature description");
        result.Entitlements.Single(e => e.FeatureKey == "enabled").Value.BooleanValue.ShouldBe(true);
        var numeric = result.Entitlements.Single(e => e.FeatureKey == "limit").Value;
        numeric.Type.ShouldBe(SubscriptionEntitlementType.Numeric);
        numeric.NumericValue.ShouldBe(0);
        numeric.IsUnlimited.ShouldBeFalse();
    }

    [Theory]
    [InlineData(SubscriptionCatalogState.Draft)]
    [InlineData(SubscriptionCatalogState.Withdrawn)]
    [InlineData(SubscriptionCatalogState.Archived)]
    public async Task Unpublished_products_and_plans_are_not_disclosed(SubscriptionCatalogState state)
    {
        var (definition, product, plan) = SeedPlan("one");
        if (state == SubscriptionCatalogState.Draft)
        {
            product = new SubscriptionProduct(product.Id, _context.TenantId, definition, "draft");
            plan = new SubscriptionPlan(plan.Id, product, "draft", "draft");
            ReturnCatalog(new[] { product }, new[] { plan });
        }
        else if (state == SubscriptionCatalogState.Withdrawn)
        {
            product.Withdraw();
            plan.Withdraw();
        }
        else
        {
            product.Archive();
            plan.Archive();
        }

        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetProductAsync(product.Id));
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetPlanAsync(plan.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Published_plan_with_unavailable_parent_is_not_disclosed(bool archived)
    {
        var (_, product, plan) = SeedPlan("one");
        if (archived) product.Archive();
        else product.Withdraw();
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetPlanAsync(plan.Id));
    }

    [Theory]
    [InlineData("plan-withdrawn")]
    [InlineData("product-archived")]
    [InlineData("plan-missing")]
    [InlineData("bundle-withdrawn")]
    public async Task Bundle_details_do_not_disclose_unavailable_components(string unavailable)
    {
        var (firstDef, firstProduct, firstPlan) = _context.Catalog("one");
        var (secondDef, secondProduct, secondPlan) = _context.Catalog("two");
        Register(firstDef);
        Register(secondDef);
        var plans = new[] { firstPlan, secondPlan };
        var products = new[] { firstProduct, secondProduct };
        var bundle = new SubscriptionBundle(Guid.NewGuid(), _context.TenantId, "bundle", "Both", plans);
        bundle.Publish(plans, products);
        ReturnCatalog(products, plans);
        _bundles.GetByIdsAsync(_context.TenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { bundle });
        if (unavailable == "plan-withdrawn") secondPlan.Withdraw();
        if (unavailable == "product-archived") secondProduct.Archive();
        if (unavailable == "plan-missing") ReturnCatalog(products, new[] { firstPlan });
        if (unavailable == "bundle-withdrawn") bundle.Withdraw();

        var error = await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetBundleAsync(bundle.Id));
        error.EntityType.ShouldBe(typeof(SubscriptionBundle));
    }

    [Fact]
    public async Task Unknown_catalog_ids_are_ABP_not_found()
    {
        var id = Guid.NewGuid();
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetProductAsync(id));
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetPlanAsync(id));
        await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetBundleAsync(id));
    }

    private (ProductDefinition, SubscriptionProduct, SubscriptionPlan) SeedPlan(string code)
    {
        var (definition, product, plan) = _context.Catalog(code);
        Register(definition);
        ReturnCatalog(new[] { product }, new[] { plan });
        return (definition, product, plan);
    }

    private void Register(ProductDefinition definition) =>
        _definitions.GetFeature(definition.Code, Arg.Any<string>())
            .Returns(call => definition.GetFeature(call.ArgAt<string>(1)));

    private void ReturnCatalog(IReadOnlyList<SubscriptionProduct> products, IReadOnlyList<SubscriptionPlan> plans)
    {
        _products.GetByIdsAsync(_context.TenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => products.Where(p => call.ArgAt<IReadOnlyCollection<Guid>>(1).Contains(p.Id)).ToArray());
        _plans.GetByIdsAsync(_context.TenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => plans.Where(p => call.ArgAt<IReadOnlyCollection<Guid>>(1).Contains(p.Id)).ToArray());
    }
}
