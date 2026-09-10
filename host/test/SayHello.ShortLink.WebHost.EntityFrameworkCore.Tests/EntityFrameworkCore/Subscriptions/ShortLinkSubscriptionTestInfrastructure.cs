using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using SayHello.ShortLink.Subscription;
using SayHello.Subscription;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.Auditing;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

[DependsOn(typeof(WebHostEntityFrameworkCoreTestModule))]
public class ShortLinkSubscriptionIntegrationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Audit saving uses a separate transaction, which cannot write before SQLite's outer transaction commits.
        Configure<AbpAuditingOptions>(options => options.IsEnabled = false);
        ShortLinkSubscriptionTestData.ConfigureExternalAdapters(context.Services);
        Configure<ShortLinkUrlOptions>(options => options.BaseUrl = "https://short.example.test");
    }
}

public static class ShortLinkSubscriptionTestData
{
    public static void ConfigureExternalAdapters(IServiceCollection services)
    {
        var dns = Substitute.For<IHostAddressResolver>();
        dns.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse("8.8.8.8")]));
        services.Replace(ServiceDescriptor.Singleton(dns));
        services.Replace(ServiceDescriptor.Singleton(Substitute.For<IShortLinkUserEligibilityChecker>()));
        services.Replace(ServiceDescriptor.Singleton(Substitute.For<IShortLinkCreationRateLimiter>()));
        services.Replace(ServiceDescriptor.Singleton(Substitute.For<IShortLinkCacheInvalidator>()));
    }

    public static async Task<T> RunAsync<T>(
        IServiceProvider root, Guid? tenantId, Guid userId, Func<IServiceProvider, Task<T>> action,
        bool commit = true)
    {
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var principal = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(AbpClaimTypes.UserId, userId.ToString("D")), new Claim(AbpClaimTypes.UserName, "bridge-test")],
                "BridgeIntegration")));
        using var unit = services.GetRequiredService<IUnitOfWorkManager>()
            .Begin(requiresNew: true, isTransactional: true);
        var result = await action(services);
        if (commit)
            await unit.CompleteAsync();
        else
            await unit.RollbackAsync();
        return result;
    }

    public static Task RunAsync(
        IServiceProvider root, Guid? tenantId, Guid userId, Func<IServiceProvider, Task> action,
        bool commit = true) =>
        RunAsync(root, tenantId, userId, async services =>
        {
            await action(services);
            return true;
        }, commit);

    public static async Task<SubscriptionPlan> CreatePlanAsync(
        IServiceProvider services, long? limit, bool? statistics, bool unlimited = false, bool setDefault = true)
    {
        var tenantId = services.GetRequiredService<ICurrentTenant>().Id;
        var definition = Definition(services);
        var products = services.GetRequiredService<ISubscriptionProductRepository>();
        var product = await products.FindByCodeAsync(definition.Code);
        if (product is null)
        {
            product = new SubscriptionProduct(Guid.NewGuid(), tenantId, definition, "Short-link integration");
            product.Publish();
            await products.InsertAsync(product, autoSave: true);
        }
        else
        {
            product.Publish();
            await products.UpdateAsync(product, autoSave: true);
        }

        var plan = new SubscriptionPlan(Guid.NewGuid(), product, "test-" + Guid.NewGuid().ToString("N"), "Integration plan");
        plan.ReplaceEntitlements(definition, Entitlements(limit, statistics, unlimited));
        plan.Publish(product, definition);
        await services.GetRequiredService<ISubscriptionPlanRepository>().InsertAsync(plan, autoSave: true);
        if (setDefault)
        {
            product.SetDefaultPlan(plan);
            await products.UpdateAsync(product, autoSave: true);
        }
        return plan;
    }

    public static async Task SetDefaultAsync(IServiceProvider services, Guid? planId)
    {
        var tenantId = services.GetRequiredService<ICurrentTenant>().Id;
        var products = services.GetRequiredService<ISubscriptionProductRepository>();
        var product = (await products.FindByCodeAsync(ShortLinkSubscriptionDefinitions.ProductCode))!;
        var plan = planId.HasValue
            ? await services.GetRequiredService<ISubscriptionPlanRepository>().GetAsync(planId.Value)
            : null;
        product.SetDefaultPlan(plan);
        await products.UpdateAsync(product, autoSave: true);
    }

    public static async Task<UserSubscription> AssignAsync(
        IServiceProvider services, Guid userId, Guid planId, DateTime? expiresAt = null)
    {
        var tenantId = services.GetRequiredService<ICurrentTenant>().Id;
        var users = services.GetRequiredService<IIdentityUserRepository>();
        if (await users.FindAsync(userId) is null)
        {
            var name = "bridge-" + userId.ToString("N");
            var user = new IdentityUser(userId, name, name + "@example.test", tenantId);
            user.SetEmailConfirmed(true);
            await users.InsertAsync(user, autoSave: true);
        }
        var manager = services.GetRequiredService<ISubscriptionManager>();
        var item = (await manager.PreviewPlanAsync(tenantId, userId, planId)).Items.Single();
        return await manager.AssignPlanAsync(new AssignSubscriptionPlan(tenantId, userId,
            new SubscriptionAssignmentTarget(item.ProductId, item.PlanId,
                item.ProductConcurrencyStamp, item.PlanConcurrencyStamp, expiresAt, item.ExpectedCurrent)));
    }

    public static ProductDefinition Definition(IServiceProvider services) =>
        services.GetRequiredService<ISubscriptionDefinitionRegistry>()
            .GetProduct(ShortLinkSubscriptionDefinitions.ProductCode);

    public static Dictionary<string, EntitlementValue> Entitlements(
        long? limit, bool? statistics, bool unlimited = false)
    {
        var values = new Dictionary<string, EntitlementValue>();
        if (unlimited || limit.HasValue)
            values[ShortLinkSubscriptionDefinitions.MaxLinks] = unlimited
                ? EntitlementValue.Unlimited()
                : EntitlementValue.Numeric(limit!.Value);
        if (statistics.HasValue)
            values[ShortLinkSubscriptionDefinitions.Statistics] = EntitlementValue.Boolean(statistics.Value);
        return values;
    }

    public static CreateShortLinkDto NewLink(DateTime? expiresAt = null) => new()
    {
        TargetUrl = "https://destination.example.test/path",
        CustomCode = "host" + Guid.NewGuid().ToString("N"),
        Title = "Host subscription integration",
        ExpiresAt = expiresAt
    };
}
