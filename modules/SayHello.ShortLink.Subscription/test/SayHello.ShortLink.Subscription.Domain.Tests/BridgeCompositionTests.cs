using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.Settings;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class BridgeCompositionTests
{
    [Fact]
    public Task ShortLink_without_bridge_keeps_settings_and_statistics() =>
        WithApplication<ShortLinkOnlyTestModule>(AssertIndependentCapabilitiesAsync);

    [Fact]
    public Task Loading_Subscription_alongside_ShortLink_does_not_enable_the_bridge() =>
        WithApplication<BothDomainsWithoutBridgeTestModule>(AssertIndependentCapabilitiesAsync);

    [Fact]
    public Task Subscription_can_initialize_without_ShortLink_or_a_product_definition() =>
        WithApplication<SubscriptionOnlyTestModule>(services =>
        {
            services.GetService<IShortLinkCapabilityProvider>().ShouldBeNull();
            services.GetRequiredService<ISubscriptionDefinitionRegistry>().GetProducts().ShouldBeEmpty();
            return Task.CompletedTask;
        });

    [Fact]
    public Task Bridge_replaces_the_default_exactly_once_and_initializes_without_a_database_or_plan() =>
        WithApplication<BridgeTestModule>(async services =>
        {
            var capability = services.GetServices<IShortLinkCapabilityProvider>().ShouldHaveSingleItem();
            capability.ShouldBeOfType<SubscriptionShortLinkCapabilityProvider>();
            capability.IsQuotaExternallyManaged.ShouldBeTrue();
            (await capability.GetQuotaAsync(null, Guid.NewGuid())).IsGranted.ShouldBeFalse();
            (await capability.IsStatisticsEnabledAsync(null, Guid.NewGuid())).ShouldBeFalse();
            services.GetService<IUserSubscriptionRepository>().ShouldBeNull();
            services.GetService<IDefaultSubscriptionPlanRepository>().ShouldBeNull();
            services.GetRequiredService<ISubscriptionDefinitionRegistry>()
                .GetProduct(BridgeTestDefinitions.ProductCode).Features.Count.ShouldBe(2);
        });

    private static async Task AssertIndependentCapabilitiesAsync(IServiceProvider services)
    {
        var capability = services.GetServices<IShortLinkCapabilityProvider>().ShouldHaveSingleItem();
        capability.ShouldBeOfType<SettingShortLinkCapabilityProvider>();
        capability.IsQuotaExternallyManaged.ShouldBeFalse();
        var userId = Guid.NewGuid();
        var quota = await capability.GetQuotaAsync(null, userId);
        quota.IsGranted.ShouldBeTrue();
        quota.IsUnlimited.ShouldBeFalse();
        quota.Limit.ShouldBe(37);
        (await capability.IsStatisticsEnabledAsync(null, userId)).ShouldBeTrue();
    }

    private static async Task WithApplication<TModule>(Func<IServiceProvider, Task> action)
        where TModule : IAbpModule
    {
        using var application = await AbpApplicationFactory.CreateAsync<TModule>(options => options.UseAutofac());
        await application.InitializeAsync();
        try
        {
            await action(application.ServiceProvider);
        }
        finally
        {
            await application.ShutdownAsync();
        }
    }
}

[DependsOn(typeof(ShortLinkDomainModule), typeof(AbpAutofacModule))]
public class ShortLinkOnlyTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(ShortLinkSettings.MaxLinksPerUser).Returns("37");
        context.Services.Replace(ServiceDescriptor.Singleton(settings));
    }
}

[DependsOn(typeof(ShortLinkOnlyTestModule), typeof(global::SayHello.Subscription.SubscriptionDomainModule))]
public class BothDomainsWithoutBridgeTestModule : AbpModule
{
}

[DependsOn(typeof(global::SayHello.Subscription.SubscriptionDomainModule), typeof(AbpAutofacModule))]
public class SubscriptionOnlyTestModule : AbpModule
{
}

[DependsOn(typeof(ShortLinkSubscriptionDomainModule), typeof(ShortLinkOnlyTestModule))]
public class BridgeTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<SubscriptionDefinitionOptions>(options => options.DefinitionProviders.Add<BridgeTestDefinitions>());
        Configure<ShortLinkSubscriptionOptions>(options =>
        {
            options.ProductCode = BridgeTestDefinitions.ProductCode;
            options.QuotaFeatureKey = BridgeTestDefinitions.QuotaFeatureKey;
            options.StatisticsFeatureKey = BridgeTestDefinitions.StatisticsFeatureKey;
        });
        var checker = Substitute.For<ISubscriptionEntitlementChecker>();
        checker.GetNumericAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(NumericEntitlementResult.NoSubscription());
        checker.GetBooleanAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BooleanEntitlementResult.NoSubscription());
        context.Services.Replace(ServiceDescriptor.Singleton(checker));
    }
}
