using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Validation;
using Xunit;

namespace SayHello.Subscription.AdminTests;

public class AdminDefaultPlanTests : SubscriptionTestBase<AdminSurfaceTestModule>
{
    private readonly HashSet<string> _granted = new()
    {
        SubscriptionAdminPermissions.Products.Default, SubscriptionAdminPermissions.Products.Update
    };
    private readonly ISubscriptionCatalogManager _catalog;
    private readonly ISubscriptionProductRepository _products;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly IProductAdminAppService _service;

    public AdminDefaultPlanTests()
    {
        var permissions = GetRequiredService<IPermissionChecker>();
        permissions.IsGrantedAsync(Arg.Any<string>()).Returns(call => _granted.Contains(call.Arg<string>()));
        permissions.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>())
            .Returns(call => _granted.Contains(call.Arg<string>()));
        _catalog = GetRequiredService<ISubscriptionCatalogManager>();
        _products = GetRequiredService<ISubscriptionProductRepository>();
        _plans = GetRequiredService<ISubscriptionPlanRepository>();
        _service = GetRequiredService<IProductAdminAppService>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Select_replace_and_clear_forward_current_tenant_stamp_and_return_current_default(bool tenant)
    {
        Guid? tenantId = tenant ? Guid.NewGuid() : null;
        var product = Product("alpha", tenantId);
        product.Publish();
        var free = Plan(product, "free");
        var replacement = Plan(product, "replacement");
        var definition = new ProductDefinition(product.Code, new FixedLocalizableString(product.Name));
        free.Publish(product, definition);
        replacement.Publish(product, definition);
        var plans = new[] { free, replacement };
        _plans.GetByIdsAsync(tenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => plans.Where(plan => call.Arg<IReadOnlyCollection<Guid>>().Contains(plan.Id)).ToArray());
        _catalog.SetDefaultPlanAsync(Arg.Is<Guid?>(id => id == tenantId), product.Id,
                Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GetRequiredService<IUnitOfWorkManager>().Current!.Options.IsTransactional.ShouldBeTrue();
                product.SetDefaultPlan(plans.SingleOrDefault(plan => plan.Id == call.ArgAt<Guid?>(3)));
                product.ConcurrencyStamp = "saved-" + call.ArgAt<string>(2);
                return product;
            });

        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            var selected = await _service.SetDefaultPlanAsync(product.Id,
                new SetDefaultPlanInputDto { ConcurrencyStamp = "original", PlanId = free.Id });
            selected.DefaultPlanId.ShouldBe(free.Id);
            selected.DefaultPlanName.ShouldBe("free");
            selected.HasDefaultPlan.ShouldBeTrue();
            selected.ConcurrencyStamp.ShouldBe("saved-original");

            var replaced = await _service.SetDefaultPlanAsync(product.Id,
                new SetDefaultPlanInputDto { ConcurrencyStamp = selected.ConcurrencyStamp, PlanId = replacement.Id });
            replaced.DefaultPlanId.ShouldBe(replacement.Id);
            replaced.DefaultPlanName.ShouldBe("replacement");
            replaced.ConcurrencyStamp.ShouldBe("saved-saved-original");

            var cleared = await _service.SetDefaultPlanAsync(product.Id,
                new SetDefaultPlanInputDto { ConcurrencyStamp = replaced.ConcurrencyStamp, PlanId = null });
            cleared.DefaultPlanId.ShouldBeNull();
            cleared.DefaultPlanName.ShouldBeNull();
            cleared.HasDefaultPlan.ShouldBeFalse();
        }
        await _catalog.Received(1).SetDefaultPlanAsync(tenantId, product.Id, "original", free.Id, Arg.Any<CancellationToken>());
        await _catalog.Received(1).SetDefaultPlanAsync(tenantId, product.Id, "saved-original", replacement.Id, Arg.Any<CancellationToken>());
        await _catalog.Received(1).SetDefaultPlanAsync(tenantId, product.Id, "saved-saved-original", null, Arg.Any<CancellationToken>());
        _catalog.ReceivedCalls().Count().ShouldBe(3);
        _plans.ReceivedCalls().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Product_results_batch_default_names_and_retain_unconfigured_state()
    {
        var tenantId = Guid.NewGuid();
        var first = Product("alpha", tenantId);
        var second = Product("beta", tenantId);
        var unconfigured = Product("gamma", tenantId);
        var free = Plan(first, "free");
        var other = Plan(second, "other");
        SetDefault(first, free.Id);
        SetDefault(second, other.Id);
        _products.GetPageAsync(Arg.Any<SubscriptionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<SubscriptionProduct>(51, new[] { first, second, unconfigured }));
        _plans.GetByIdsAsync(tenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { free, other });

        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            var result = await _service.GetListAsync(new AdminCatalogQueryDto { SkipCount = 20, MaxResultCount = 10 });
            result.TotalCount.ShouldBe(51);
            result.Items[0].DefaultPlanId.ShouldBe(free.Id);
            result.Items[0].DefaultPlanName.ShouldBe("free");
            result.Items[1].DefaultPlanName.ShouldBe("other");
            result.Items[2].HasDefaultPlan.ShouldBeFalse();
            result.Items[2].DefaultPlanId.ShouldBeNull();
            result.Items[2].DefaultPlanName.ShouldBeNull();
        }
        await _plans.Received(1).GetByIdsAsync(tenantId,
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(free.Id) && ids.Contains(other.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Product_detail_includes_default_name_but_does_not_mislabel_invalid_product_reference()
    {
        var product = Product("alpha");
        var otherProduct = Product("beta");
        var free = Plan(product, "free");
        SetDefault(product, free.Id);
        _products.GetByIdsAsync(null, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { product });
        _plans.GetByIdsAsync(null, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { free });
        var dto = await _service.GetAsync(product.Id);
        dto.HasDefaultPlan.ShouldBeTrue();
        dto.DefaultPlanName.ShouldBe("free");

        var wrongProductPlan = Plan(otherProduct, "other");
        SetDefault(product, wrongProductPlan.Id);
        _plans.GetByIdsAsync(null, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { wrongProductPlan });
        dto = await _service.GetAsync(product.Id);
        dto.HasDefaultPlan.ShouldBeTrue();
        dto.DefaultPlanId.ShouldBe(wrongProductPlan.Id);
        dto.DefaultPlanName.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Picker_forces_route_product_current_tenant_and_publication_before_repository_paging(bool tenant)
    {
        Guid? tenantId = tenant ? Guid.NewGuid() : null;
        var product = Product("alpha", tenantId);
        product.Publish();
        var free = Plan(product, "free");
        free.Publish(product, new ProductDefinition(product.Code, new FixedLocalizableString(product.Name)));
        _products.GetByIdsAsync(tenantId, Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(product.Id)),
            Arg.Any<CancellationToken>()).Returns(new[] { product });
        _plans.GetPageAsync(Arg.Any<SubscriptionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<SubscriptionPlan>(37, new[] { free }));

        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            var page = await _service.GetDefaultPlanOptionsAsync(product.Id, new AdminCatalogQueryDto
            {
                ProductId = Guid.NewGuid(), State = SubscriptionCatalogState.Draft, Filter = "free",
                SkipCount = 20, MaxResultCount = 10, Sorting = SubscriptionCatalogSort.NameDescending
            });
            page.TotalCount.ShouldBe(37);
            page.Items.Single().Id.ShouldBe(free.Id);
            page.Items.Single().ProductId.ShouldBe(product.Id);
        }
        await _plans.Received(1).GetPageAsync(Arg.Is<SubscriptionCatalogQuery>(query =>
            query.TenantId == tenantId && query.ProductId == product.Id && query.PublishedOnly &&
            query.State == SubscriptionCatalogState.Published && query.Filter == "free" &&
            query.SkipCount == 20 && query.MaxResultCount == 10 && query.Sorting == SubscriptionCatalogSort.NameDescending),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SubscriptionCatalogState.Draft)]
    [InlineData(SubscriptionCatalogState.Withdrawn)]
    [InlineData(SubscriptionCatalogState.Archived)]
    public async Task Picker_does_not_return_options_for_an_unpublished_product(SubscriptionCatalogState state)
    {
        var product = Product("alpha");
        if (state == SubscriptionCatalogState.Withdrawn) product.Withdraw();
        if (state == SubscriptionCatalogState.Archived) product.Archive();
        _products.GetByIdsAsync(null, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { product });

        var page = await _service.GetDefaultPlanOptionsAsync(product.Id, new AdminCatalogQueryDto());
        page.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
        _plans.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Picker_rejects_products_outside_current_tenant_even_with_data_filter_disabled()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        _products.GetByIdsAsync(tenantId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SubscriptionProduct>());
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (GetRequiredService<IDataFilter>().Disable<IMultiTenant>())
            await Should.ThrowAsync<EntityNotFoundException>(() =>
                _service.GetDefaultPlanOptionsAsync(productId, new AdminCatalogQueryDto()));
        await _products.Received(1).GetByIdsAsync(tenantId,
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(productId)), Arg.Any<CancellationToken>());
        _plans.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Default_selection_requires_product_update_and_reads_require_product_access()
    {
        _granted.Remove(SubscriptionAdminPermissions.Products.Update);
        await Should.ThrowAsync<AbpAuthorizationException>(() => _service.SetDefaultPlanAsync(Guid.NewGuid(),
            new SetDefaultPlanInputDto { ConcurrencyStamp = "stamp", PlanId = Guid.NewGuid() }));
        await Should.ThrowAsync<AbpAuthorizationException>(() => _service.SetDefaultPlanAsync(Guid.NewGuid(),
            new SetDefaultPlanInputDto { ConcurrencyStamp = "stamp", PlanId = null }));
        _granted.Clear();
        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            _service.GetDefaultPlanOptionsAsync(Guid.NewGuid(), new AdminCatalogQueryDto()));
        _catalog.ReceivedCalls().ShouldBeEmpty();
        _products.ReceivedCalls().ShouldBeEmpty();
        _plans.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Default_selection_requires_a_stamp_and_nonempty_plan_and_picker_validates_page_size()
    {
        await Should.ThrowAsync<AbpValidationException>(() =>
            _service.SetDefaultPlanAsync(Guid.NewGuid(), new SetDefaultPlanInputDto { PlanId = Guid.NewGuid() }));
        await Should.ThrowAsync<AbpValidationException>(() => _service.SetDefaultPlanAsync(Guid.NewGuid(),
            new SetDefaultPlanInputDto { ConcurrencyStamp = "stamp", PlanId = Guid.Empty }));
        await Should.ThrowAsync<AbpValidationException>(() =>
            _service.GetDefaultPlanOptionsAsync(Guid.NewGuid(), new AdminCatalogQueryDto { MaxResultCount = 101 }));
        _catalog.ReceivedCalls().ShouldBeEmpty();
        _products.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SubscriptionErrorCodes.ConcurrencyConflict)]
    [InlineData(SubscriptionErrorCodes.InvalidDefaultPlan)]
    [InlineData(SubscriptionErrorCodes.DefaultPlanInUse)]
    public async Task Default_business_failures_are_not_retried_or_translated_to_success(string code)
    {
        _catalog.SetDefaultPlanAsync(Arg.Is<Guid?>(id => id == null), Arg.Any<Guid>(), "stale",
                Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns<Task<SubscriptionProduct>>(_ => throw new BusinessException(code));
        var error = await Should.ThrowAsync<BusinessException>(() => _service.SetDefaultPlanAsync(Guid.NewGuid(),
            new SetDefaultPlanInputDto { ConcurrencyStamp = "stale", PlanId = Guid.NewGuid() }));
        error.Code.ShouldBe(code);
        _catalog.ReceivedCalls().Count().ShouldBe(1);
        _products.ReceivedCalls().ShouldBeEmpty();
        _plans.ReceivedCalls().ShouldBeEmpty();
    }

    private static SubscriptionProduct Product(string code, Guid? tenantId = null) =>
        new(Guid.NewGuid(), tenantId, new ProductDefinition(code, new FixedLocalizableString(code)), code);

    private static SubscriptionPlan Plan(SubscriptionProduct product, string code) =>
        new(Guid.NewGuid(), product, code, code);

    private static void SetDefault(SubscriptionProduct product, Guid? planId) =>
        typeof(SubscriptionProduct).GetProperty(nameof(SubscriptionProduct.DefaultPlanId))!.SetValue(product, planId);
}
