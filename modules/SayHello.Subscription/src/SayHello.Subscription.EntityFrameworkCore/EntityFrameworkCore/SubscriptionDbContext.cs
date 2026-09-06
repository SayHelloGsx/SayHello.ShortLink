using Microsoft.EntityFrameworkCore;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.Subscription.EntityFrameworkCore;

[ConnectionStringName(SubscriptionDbProperties.ConnectionStringName)]
public class SubscriptionDbContext : AbpDbContext<SubscriptionDbContext>, ISubscriptionDbContext
{
    public DbSet<SubscriptionProduct> SubscriptionProducts => Set<SubscriptionProduct>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SubscriptionPlanEntitlement> SubscriptionPlanEntitlements => Set<SubscriptionPlanEntitlement>();
    public DbSet<SubscriptionBundle> SubscriptionBundles => Set<SubscriptionBundle>();
    public DbSet<SubscriptionBundleItem> SubscriptionBundleItems => Set<SubscriptionBundleItem>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<UserSubscriptionEntitlement> UserSubscriptionEntitlements => Set<UserSubscriptionEntitlement>();

    public SubscriptionDbContext(DbContextOptions<SubscriptionDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureSubscription();
    }
}
