using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Public.Entitlements;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Users;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class BridgeCompositionTests
{
    [Fact]
    public Task ShortLink_without_bridge_keeps_its_setting_backed_capabilities() =>
        WithApplication<ShortLinkOnlyTestModule>(AssertIndependentCapabilitiesAsync);

    [Fact]
    public Task Loading_only_subscription_contracts_does_not_enable_the_bridge() =>
        WithApplication<ShortLinkAndSubscriptionContractsTestModule>(AssertIndependentCapabilitiesAsync);

    [Fact]
    public Task Bridge_replaces_the_default_once_and_registers_its_definition() =>
        WithApplication<BridgeContractTestModule>(async services =>
        {
            var capability = services.GetServices<IShortLinkCapabilityProvider>().ShouldHaveSingleItem();
            capability.ShouldBeOfType<SubscriptionShortLinkCapabilityProvider>();
            capability.IsQuotaExternallyManaged.ShouldBeTrue();
            (await capability.GetQuotaAsync(BridgeTestSubject.TenantId, BridgeTestSubject.UserId))
                .Limit.ShouldBe(20);
            (await capability.IsStatisticsEnabledAsync(BridgeTestSubject.TenantId, BridgeTestSubject.UserId))
                .ShouldBeTrue();
            services.GetRequiredService<ISubscriptionDefinitionRegistry>()
                .GetProduct(ShortLinkSubscriptionDefinitions.ProductCode).Features.Count.ShouldBe(2);
        });

    private static async Task AssertIndependentCapabilitiesAsync(IServiceProvider services)
    {
        var capability = services.GetServices<IShortLinkCapabilityProvider>().ShouldHaveSingleItem();
        capability.ShouldBeOfType<SettingShortLinkCapabilityProvider>();
        capability.IsQuotaExternallyManaged.ShouldBeFalse();
        var quota = await capability.GetQuotaAsync(null, Guid.NewGuid());
        quota.IsGranted.ShouldBeTrue();
        quota.IsUnlimited.ShouldBeFalse();
        quota.Limit.ShouldBe(37);
        (await capability.IsStatisticsEnabledAsync(null, Guid.NewGuid())).ShouldBeTrue();
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

public static class BridgeTestSubject
{
    public static readonly Guid UserId = Guid.NewGuid();
    public static readonly Guid TenantId = Guid.NewGuid();
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

[DependsOn(
    typeof(ShortLinkOnlyTestModule),
    typeof(global::SayHello.Subscription.Public.SubscriptionPublicApplicationContractsModule))]
public class ShortLinkAndSubscriptionContractsTestModule : AbpModule;

[DependsOn(typeof(ShortLinkSubscriptionApplicationModule), typeof(AbpAutofacModule))]
public class BridgeContractTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var client = Substitute.For<ICurrentUserEntitlementAppService>();
        client.GetNumericAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.MaxLinks,
                Arg.Any<CancellationToken>())
            .Returns(new NumericEntitlementResultDto { IsGranted = true, Limit = 20 });
        client.GetBooleanAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics,
                Arg.Any<CancellationToken>())
            .Returns(new BooleanEntitlementResultDto { IsGranted = true });
        context.Services.Replace(ServiceDescriptor.Singleton(client));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.Id.Returns(BridgeTestSubject.UserId);
        context.Services.Replace(ServiceDescriptor.Singleton(currentUser));

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(BridgeTestSubject.TenantId);
        context.Services.Replace(ServiceDescriptor.Singleton(currentTenant));
    }
}
