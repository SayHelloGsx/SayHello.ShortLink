using Microsoft.EntityFrameworkCore;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.Subscription.EntityFrameworkCore;

[ConnectionStringName(SubscriptionDbProperties.ConnectionStringName)]
public interface ISubscriptionDbContext : IEfCoreDbContext
{
    DbSet<SubscriptionProduct> SubscriptionProducts { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<SubscriptionPlanEntitlement> SubscriptionPlanEntitlements { get; }
    DbSet<SubscriptionBundle> SubscriptionBundles { get; }
    DbSet<SubscriptionBundleItem> SubscriptionBundleItems { get; }
    DbSet<UserSubscription> UserSubscriptions { get; }
    DbSet<UserSubscriptionEntitlement> UserSubscriptionEntitlements { get; }
}
