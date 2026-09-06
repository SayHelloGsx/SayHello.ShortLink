using Microsoft.Extensions.DependencyInjection;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace SayHello.Subscription.EntityFrameworkCore;

[DependsOn(typeof(SubscriptionDomainModule), typeof(AbpEntityFrameworkCoreModule))]
public class SubscriptionEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<SubscriptionDbContext>(options =>
        {
            options.AddRepository<SubscriptionProduct, EfCoreSubscriptionProductRepository>();
            options.AddRepository<SubscriptionPlan, EfCoreSubscriptionPlanRepository>();
            options.AddRepository<SubscriptionBundle, EfCoreSubscriptionBundleRepository>();
            options.AddRepository<UserSubscription, EfCoreUserSubscriptionRepository>();
        });
    }
}
