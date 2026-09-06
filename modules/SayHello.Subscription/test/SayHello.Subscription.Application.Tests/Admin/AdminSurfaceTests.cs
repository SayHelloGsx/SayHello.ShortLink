using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SayHello.Subscription.Admin;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Permissions;
using SayHello.Subscription.Admin.Users;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using SayHello.Subscription.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Validation;
using Xunit;

namespace SayHello.Subscription.AdminTests;

[DependsOn(typeof(SubscriptionAdminApplicationModule), typeof(SubscriptionTestBaseModule))]
public class AdminSurfaceTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton(Substitute.For<ISubscriptionProductRepository>());
        context.Services.AddSingleton(Substitute.For<ISubscriptionPlanRepository>());
        context.Services.AddSingleton(Substitute.For<ISubscriptionBundleRepository>());
        context.Services.AddSingleton(Substitute.For<IUserSubscriptionRepository>());
        context.Services.AddSingleton(Substitute.For<ISubscriptionCatalogManager>());
        context.Services.AddSingleton(Substitute.For<ISubscriptionManager>());
        context.Services.AddSingleton(Substitute.For<ISubscriptionUserDirectory>());
        context.Services.AddSingleton(Substitute.For<ISubscriptionDefinitionRegistry>());
        context.Services.AddSingleton(Substitute.For<IPermissionChecker>());
        Configure<AbpClockOptions>(options => options.Kind = DateTimeKind.Local);
    }
}

public class AdminSurfaceTests : SubscriptionTestBase<AdminSurfaceTestModule>
{
    private readonly IPermissionChecker _permissions;
    private readonly HashSet<string> _granted = new();
    private readonly ISubscriptionManager _manager;
    private readonly ISubscriptionCatalogManager _catalog;

    public AdminSurfaceTests()
    {
        _permissions = GetRequiredService<IPermissionChecker>();
        _permissions.IsGrantedAsync(Arg.Any<string>()).Returns(call => _granted.Contains(call.Arg<string>()));
        _permissions.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>())
            .Returns(call => _granted.Contains(call.Arg<string>()));
        _manager = GetRequiredService<ISubscriptionManager>();
        _catalog = GetRequiredService<ISubscriptionCatalogManager>();
    }

    [Fact]
    public async Task Catalog_read_returns_total_count_and_propagates_tenant_filters_and_paging()
    {
        _granted.Add(SubscriptionAdminPermissions.Products.Default);
        var tenantId = Guid.NewGuid();
        var product = Product("alpha", tenantId);
        var repository = GetRequiredService<ISubscriptionProductRepository>();
        repository.GetPageAsync(Arg.Any<SubscriptionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<SubscriptionProduct>(51, new[] { product }));
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            var page = await GetRequiredService<IProductAdminAppService>().GetListAsync(new AdminCatalogQueryDto
            {
                Filter = "alpha", State = SubscriptionCatalogState.Draft, SkipCount = 20,
                MaxResultCount = 10, Sorting = SubscriptionCatalogSort.NameDescending
            });
            page.TotalCount.ShouldBe(51);
            page.Items.Single().ConcurrencyStamp.ShouldBe(product.ConcurrencyStamp);
        }
        await repository.Received(1).GetPageAsync(Arg.Is<SubscriptionCatalogQuery>(q =>
            q.TenantId == tenantId && q.Filter == "alpha" && q.SkipCount == 20 && q.MaxResultCount == 10 &&
            q.State == SubscriptionCatalogState.Draft && q.Sorting == SubscriptionCatalogSort.NameDescending && !q.PublishedOnly),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Read_permission_does_not_allow_writes()
    {
        _granted.UnionWith(new[]
        {
            SubscriptionAdminPermissions.Products.Default, SubscriptionAdminPermissions.Plans.Default,
            SubscriptionAdminPermissions.Bundles.Default, SubscriptionAdminPermissions.Users.Default
        });
        var products = GetRequiredService<IProductAdminAppService>();
        var plans = GetRequiredService<IPlanAdminAppService>();
        var bundles = GetRequiredService<IBundleAdminAppService>();
        var users = GetRequiredService<IUserSubscriptionAdminAppService>();
        var id = Guid.NewGuid();
        var commands = new Func<Task>[]
        {
            () => products.CreateAsync(new CreateProductDto { Code = "alpha", Name = "Alpha" }),
            () => products.UpdateAsync(id, new UpdateProductDto { Name = "Alpha", ConcurrencyStamp = "stamp" }),
            () => products.DeleteAsync(id, new VersionInputDto { ConcurrencyStamp = "stamp" }),
            () => products.SetStateAsync(id, new CatalogStateInputDto { State = SubscriptionCatalogState.Published, ConcurrencyStamp = "stamp" }),
            () => plans.CreateAsync(new CreatePlanDto { Code = "basic", Name = "Basic", ProductId = id }),
            () => plans.UpdateAsync(id, new UpdatePlanDto { Name = "Basic", ConcurrencyStamp = "stamp" }),
            () => plans.DeleteAsync(id, new VersionInputDto { ConcurrencyStamp = "stamp" }),
            () => plans.SetStateAsync(id, new CatalogStateInputDto { State = SubscriptionCatalogState.Published, ConcurrencyStamp = "stamp" }),
            () => bundles.CreateAsync(new CreateBundleDto { Code = "suite", Name = "Suite", PlanIds = new() { id, Guid.NewGuid() } }),
            () => bundles.UpdateAsync(id, new UpdateBundleDto { Name = "Suite", ConcurrencyStamp = "stamp", PlanIds = new() { id, Guid.NewGuid() } }),
            () => bundles.DeleteAsync(id, new VersionInputDto { ConcurrencyStamp = "stamp" }),
            () => bundles.SetStateAsync(id, new CatalogStateInputDto { State = SubscriptionCatalogState.Published, ConcurrencyStamp = "stamp" }),
            () => users.LookupUsersAsync(new UserLookupInputDto()),
            () => users.PreviewPlanAsync(id, Guid.NewGuid()),
            () => users.PreviewBundleAsync(id, Guid.NewGuid()),
            () => users.AssignPlanAsync(new AssignPlanDto { UserId = id, Target = Target() }),
            () => users.AssignBundleAsync(new AssignBundleDto { UserId = id, BundleId = Guid.NewGuid(),
                BundleConcurrencyStamp = "bundle", Targets = new() { Target(), Target() } }),
            () => users.RevokeAsync(id, new RevokeSubscriptionDto { ConcurrencyStamp = "stamp" }),
            () => users.AdjustExpirationAsync(id, new AdjustExpirationDto { ConcurrencyStamp = "stamp" })
        };
        foreach (var command in commands) await Should.ThrowAsync<AbpAuthorizationException>(command);
        _manager.ReceivedCalls().ShouldBeEmpty();
        _catalog.ReceivedCalls().ShouldBeEmpty();
        GetRequiredService<ISubscriptionUserDirectory>().ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Bundle_assignment_is_one_atomic_command_with_independent_expirations_and_all_versions()
    {
        _granted.UnionWith(new[] { SubscriptionAdminPermissions.Users.Default, SubscriptionAdminPermissions.Users.Assign });
        var tenantId = Guid.NewGuid();
        var first = Target();
        first.ExpiresAt = DateTime.UtcNow.AddDays(5);
        first.ExpectedCurrent = new SubscriptionVersionDto { SubscriptionId = Guid.NewGuid(), ConcurrencyStamp = "old" };
        var second = Target();
        var input = new AssignBundleDto
        {
            UserId = Guid.NewGuid(), BundleId = Guid.NewGuid(), BundleConcurrencyStamp = "bundle-stamp",
            Targets = new() { first, second }
        };
        _manager.AssignBundleAsync(Arg.Any<AssignSubscriptionBundle>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UserSubscription>());
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            (await GetRequiredService<IUserSubscriptionAdminAppService>().AssignBundleAsync(input)).Items.ShouldBeEmpty();

        var command = (AssignSubscriptionBundle)_manager.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(ISubscriptionManager.AssignBundleAsync)).GetArguments()[0]!;
        command.TenantId.ShouldBe(tenantId);
        command.UserId.ShouldBe(input.UserId);
        command.BundleId.ShouldBe(input.BundleId);
        command.BundleConcurrencyStamp.ShouldBe("bundle-stamp");
        command.Targets[0].ExpiresAt.ShouldBe(first.ExpiresAt);
        command.Targets[0].ExpectedCurrent!.SubscriptionId.ShouldBe(first.ExpectedCurrent.SubscriptionId);
        command.Targets[0].ExpectedCurrent!.ConcurrencyStamp.ShouldBe("old");
        command.Targets[0].ProductConcurrencyStamp.ShouldBe(first.ProductConcurrencyStamp);
        command.Targets[0].PlanConcurrencyStamp.ShouldBe(first.PlanConcurrencyStamp);
        command.Targets[1].ExpiresAt.ShouldBeNull();
        command.Targets[1].ExpectedCurrent.ShouldBeNull();
        await _manager.DidNotReceive().AssignPlanAsync(Arg.Any<AssignSubscriptionPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_preserves_product_plan_and_current_subscription_stamps()
    {
        _granted.UnionWith(new[] { SubscriptionAdminPermissions.Users.Default, SubscriptionAdminPermissions.Users.Assign });
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var current = new SubscriptionVersion(Guid.NewGuid(), "old");
        _manager.PreviewPlanAsync(null, userId, planId, Arg.Any<CancellationToken>()).Returns(
            new SubscriptionAssignmentPreview(null, userId, null, null, new[]
            {
                new SubscriptionAssignmentPreviewItem(Guid.NewGuid(), "alpha", "Alpha", "product",
                    planId, "basic", "Basic", "plan", current, DateTime.UtcNow.AddDays(1), Array.Empty<EntitlementSnapshotData>())
            }));
        var result = await GetRequiredService<IUserSubscriptionAdminAppService>().PreviewPlanAsync(userId, planId);
        var target = result.Items.Single();
        target.ProductConcurrencyStamp.ShouldBe("product");
        target.PlanConcurrencyStamp.ShouldBe("plan");
        target.ExpectedCurrent!.SubscriptionId.ShouldBe(current.SubscriptionId);
        target.ExpectedCurrent.ConcurrencyStamp.ShouldBe(current.ConcurrencyStamp);
        target.ExpiresAt.ShouldBeNull();
    }

    [Fact]
    public async Task Subscription_reads_normalize_clock_to_utc_and_use_materialized_repository_paging()
    {
        _granted.Add(SubscriptionAdminPermissions.Users.Default);
        var userId = Guid.NewGuid();
        var repository = GetRequiredService<IUserSubscriptionRepository>();
        repository.GetPageAsync(Arg.Any<UserSubscriptionQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionPage<UserSubscription>(123, Array.Empty<UserSubscription>()));
        var result = await GetRequiredService<IUserSubscriptionAdminAppService>().GetListAsync(new AdminSubscriptionQueryDto
        {
            UserId = userId, Status = UserSubscriptionStatus.Expired, CurrentOnly = true, SkipCount = 40, MaxResultCount = 20
        });
        result.TotalCount.ShouldBe(123);
        await repository.Received(1).GetPageAsync(Arg.Is<UserSubscriptionQuery>(q =>
            q.UserId == userId && q.Status == UserSubscriptionStatus.Expired && q.CurrentOnly &&
            q.SkipCount == 40 && q.MaxResultCount == 20 && q.Now.Kind == DateTimeKind.Utc), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stale_assignment_failure_is_not_translated_to_success()
    {
        _granted.UnionWith(new[] { SubscriptionAdminPermissions.Users.Default, SubscriptionAdminPermissions.Users.Assign });
        _manager.AssignPlanAsync(Arg.Any<AssignSubscriptionPlan>(), Arg.Any<CancellationToken>())
            .Returns<Task<UserSubscription>>(_ => throw new BusinessException(SubscriptionErrorCodes.ConcurrencyConflict));
        var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IUserSubscriptionAdminAppService>()
            .AssignPlanAsync(new AssignPlanDto { UserId = Guid.NewGuid(), Target = Target() }));
        error.Code.ShouldBe(SubscriptionErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task Dto_validation_rejects_duplicate_bundle_targets_empty_users_and_oversized_pages()
    {
        _granted.UnionWith(new[] { SubscriptionAdminPermissions.Users.Default, SubscriptionAdminPermissions.Users.Assign,
            SubscriptionAdminPermissions.Products.Default });
        var users = GetRequiredService<IUserSubscriptionAdminAppService>();
        var target = Target();
        await Should.ThrowAsync<AbpValidationException>(() => users.AssignBundleAsync(new AssignBundleDto
        {
            UserId = Guid.NewGuid(), BundleId = Guid.NewGuid(), BundleConcurrencyStamp = "bundle",
            Targets = new() { target, target }
        }));
        await Should.ThrowAsync<AbpValidationException>(() => users.AssignPlanAsync(new AssignPlanDto { Target = Target() }));
        await Should.ThrowAsync<AbpValidationException>(() => GetRequiredService<IProductAdminAppService>()
            .GetListAsync(new AdminCatalogQueryDto { MaxResultCount = 101 }));
        _manager.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Plan_configuration_preserves_boolean_zero_int64_and_explicit_unlimited_values()
    {
        _granted.UnionWith(new[] { SubscriptionAdminPermissions.Plans.Default, SubscriptionAdminPermissions.Plans.Create });
        var definition = new ProductDefinition("alpha", new FixedLocalizableString("Alpha"), new[]
        {
            new FeatureDefinition("enabled", new FixedLocalizableString("Enabled"), SubscriptionEntitlementType.Boolean),
            new FeatureDefinition("zero", new FixedLocalizableString("Zero"), SubscriptionEntitlementType.Numeric),
            new FeatureDefinition("finite", new FixedLocalizableString("Finite"), SubscriptionEntitlementType.Numeric),
            new FeatureDefinition("unlimited", new FixedLocalizableString("Unlimited"), SubscriptionEntitlementType.Numeric, allowUnlimited: true)
        });
        var product = new SubscriptionProduct(Guid.NewGuid(), null, definition, "Alpha");
        var plan = new SubscriptionPlan(Guid.NewGuid(), product, "basic", "Basic");
        GetRequiredService<ISubscriptionProductRepository>().GetByIdsAsync(null, Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>()).Returns(new[] { product });
        _catalog.CreatePlanAsync(null, product.Id, "basic", Arg.Any<CatalogDetails>(),
            Arg.Any<IReadOnlyDictionary<string, EntitlementValue>>(), Arg.Any<CancellationToken>()).Returns(plan);
        await GetRequiredService<IPlanAdminAppService>().CreateAsync(new CreatePlanDto
        {
            ProductId = product.Id, Code = "basic", Name = "Basic",
            Entitlements = new()
            {
                new() { FeatureKey = "enabled", Value = new EntitlementValueDto { Type = SubscriptionEntitlementType.Boolean, BooleanValue = true } },
                new() { FeatureKey = "zero", Value = new EntitlementValueDto { Type = SubscriptionEntitlementType.Numeric, NumericValue = 0 } },
                new() { FeatureKey = "finite", Value = new EntitlementValueDto { Type = SubscriptionEntitlementType.Numeric, NumericValue = long.MaxValue } },
                new() { FeatureKey = "unlimited", Value = new EntitlementValueDto { Type = SubscriptionEntitlementType.Numeric, IsUnlimited = true } }
            }
        });
        var values = (IReadOnlyDictionary<string, EntitlementValue>)_catalog.ReceivedCalls().Single().GetArguments()[4]!;
        values["enabled"].BooleanValue.ShouldBe(true);
        values["zero"].NumericValue.ShouldBe(0);
        values["finite"].NumericValue.ShouldBe(long.MaxValue);
        values["unlimited"].IsUnlimited.ShouldBeTrue();
        values["unlimited"].NumericValue.ShouldBeNull();
    }

    [Fact]
    public async Task Revoke_and_expiration_commands_preserve_tenant_stamp_and_reason()
    {
        _granted.UnionWith(new[] { SubscriptionAdminPermissions.Users.Default,
            SubscriptionAdminPermissions.Users.Revoke, SubscriptionAdminPermissions.Users.AdjustExpiration });
        var tenantId = Guid.NewGuid();
        var product = Product("alpha", tenantId);
        product.Publish();
        var definition = new ProductDefinition("alpha", new FixedLocalizableString("Alpha"));
        var plan = new SubscriptionPlan(Guid.NewGuid(), product, "basic", "Basic");
        plan.Publish(product, definition);
        var subscription = new UserSubscription(Guid.NewGuid(), Guid.NewGuid(), product, plan,
            Array.Empty<EntitlementSnapshotData>(), DateTime.UtcNow, null, Guid.NewGuid());
        var expires = DateTime.UtcNow.AddMonths(1);
        _manager.RevokeAsync(tenantId, subscription.Id, "original", "requested", Arg.Any<CancellationToken>()).Returns(subscription);
        _manager.AdjustExpirationAsync(tenantId, subscription.Id, "original", expires, Arg.Any<CancellationToken>()).Returns(subscription);
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            var service = GetRequiredService<IUserSubscriptionAdminAppService>();
            (await service.RevokeAsync(subscription.Id, new RevokeSubscriptionDto
            {
                ConcurrencyStamp = "original", Reason = "requested"
            })).UserId.ShouldBe(subscription.UserId);
            (await service.AdjustExpirationAsync(subscription.Id, new AdjustExpirationDto
            {
                ConcurrencyStamp = "original", ExpiresAt = expires
            })).ConcurrencyStamp.ShouldBe(subscription.ConcurrencyStamp);
        }
        await _manager.Received(1).RevokeAsync(tenantId, subscription.Id, "original", "requested", Arg.Any<CancellationToken>());
        await _manager.Received(1).AdjustExpirationAsync(tenantId, subscription.Id, "original", expires, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(typeof(ProductAdminAppService), "CreateAsync", SubscriptionAdminPermissions.Products.Create)]
    [InlineData(typeof(PlanAdminAppService), "UpdateAsync", SubscriptionAdminPermissions.Plans.Update)]
    [InlineData(typeof(BundleAdminAppService), "SetStateAsync", SubscriptionAdminPermissions.Bundles.Publish)]
    [InlineData(typeof(UserSubscriptionAdminAppService), "RevokeAsync", SubscriptionAdminPermissions.Users.Revoke)]
    [InlineData(typeof(UserSubscriptionAdminAppService), "AdjustExpirationAsync", SubscriptionAdminPermissions.Users.AdjustExpiration)]
    public void Mutation_methods_are_virtual_and_carry_granular_policy(Type service, string methodName, string permission)
    {
        var method = service.GetMethod(methodName)!;
        method.IsVirtual.ShouldBeTrue();
        method.GetCustomAttributes<AuthorizeAttribute>().Single().Policy.ShouldBe(permission);
        method.GetCustomAttribute<UnitOfWorkAttribute>()!.IsTransactional.ShouldBe(true);
    }

    private static AssignmentTargetDto Target() => new()
    {
        ProductId = Guid.NewGuid(), PlanId = Guid.NewGuid(), ProductConcurrencyStamp = "product",
        PlanConcurrencyStamp = "plan"
    };

    private static SubscriptionProduct Product(string code, Guid? tenantId = null) =>
        new(Guid.NewGuid(), tenantId, new ProductDefinition(code, new FixedLocalizableString(code)), code);
}
