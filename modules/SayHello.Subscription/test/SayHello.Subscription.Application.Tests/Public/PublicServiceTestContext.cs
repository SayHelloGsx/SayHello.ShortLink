using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace SayHello.Subscription.Public;

internal sealed class PublicServiceTestContext
{
    public DateTime Now { get; } = new(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
    public Guid UserId { get; } = Guid.NewGuid();
    public Guid? TenantId { get; } = Guid.NewGuid();
    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();
    public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();
    public IClock Clock { get; } = Substitute.For<IClock>();

    public PublicServiceTestContext()
    {
        CurrentUser.Id.Returns(UserId);
        CurrentUser.IsAuthenticated.Returns(true);
        CurrentTenant.Id.Returns(TenantId);
        Clock.Now.Returns(Now);
    }

    public T Configure<T>(T service) where T : SubscriptionApplicationService
    {
        var lazy = Substitute.For<IAbpLazyServiceProvider>();
        lazy.LazyGetRequiredService<ICurrentUser>().Returns(CurrentUser);
        lazy.LazyGetRequiredService<ICurrentTenant>().Returns(CurrentTenant);
        lazy.LazyGetRequiredService<IClock>().Returns(Clock);
        lazy.LazyGetRequiredService<ICancellationTokenProvider>().Returns(Substitute.For<ICancellationTokenProvider>());
        service.LazyServiceProvider = lazy;
        return service;
    }

    public (ProductDefinition Definition, SubscriptionProduct Product, SubscriptionPlan Plan) Catalog(string code)
    {
        var definition = new ProductDefinition(code, new FixedLocalizableString(code), new[]
        {
            new FeatureDefinition("enabled", new FixedLocalizableString("Enabled feature"),
                SubscriptionEntitlementType.Boolean, description: new FixedLocalizableString("Feature description")),
            new FeatureDefinition("limit", new FixedLocalizableString("Numeric limit"),
                SubscriptionEntitlementType.Numeric, allowUnlimited: true)
        });
        var product = new SubscriptionProduct(Guid.NewGuid(), TenantId, definition, code + " product");
        product.Publish();
        var plan = new SubscriptionPlan(Guid.NewGuid(), product, "standard", code + " plan");
        plan.ReplaceEntitlements(definition, new Dictionary<string, EntitlementValue>
        {
            ["enabled"] = EntitlementValue.Boolean(true),
            ["limit"] = EntitlementValue.Numeric(0)
        });
        plan.Publish(product, definition);
        return (definition, product, plan);
    }

    public UserSubscription Assign(SubscriptionProduct product, SubscriptionPlan plan, DateTime? expiresAt = null,
        SubscriptionBundle? bundle = null, Guid? assignmentId = null, Guid? userId = null) =>
        new(Guid.NewGuid(), userId ?? UserId, product, plan,
            plan.Entitlements.Select(e => new EntitlementSnapshotData(e.FeatureKey,
                "Snapshot " + e.FeatureKey, e.ToValue())).ToArray(), Now.AddDays(-1), expiresAt,
            assignmentId ?? Guid.NewGuid(), bundle);
}
