using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using SayHello.Subscription;
using SayHello.Subscription.Definitions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class ShortLinkSubscriptionOptionsTests
{
    [Theory]
    [InlineData(nameof(ShortLinkSubscriptionOptions.ProductCode), null)]
    [InlineData(nameof(ShortLinkSubscriptionOptions.ProductCode), "")]
    [InlineData(nameof(ShortLinkSubscriptionOptions.ProductCode), " ")]
    [InlineData(nameof(ShortLinkSubscriptionOptions.QuotaFeatureKey), null)]
    [InlineData(nameof(ShortLinkSubscriptionOptions.QuotaFeatureKey), "")]
    [InlineData(nameof(ShortLinkSubscriptionOptions.QuotaFeatureKey), " ")]
    [InlineData(nameof(ShortLinkSubscriptionOptions.StatisticsFeatureKey), null)]
    [InlineData(nameof(ShortLinkSubscriptionOptions.StatisticsFeatureKey), "")]
    [InlineData(nameof(ShortLinkSubscriptionOptions.StatisticsFeatureKey), " ")]
    public void All_mapping_keys_are_required_before_any_definition_lookup(string property, string? value)
    {
        var definitions = Substitute.For<ISubscriptionDefinitionRegistry>();
        var options = BridgeTestDefinitions.Options();
        typeof(ShortLinkSubscriptionOptions).GetProperty(property)!.SetValue(options, value);

        var result = new ShortLinkSubscriptionOptionsValidator(definitions).Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(property);
        definitions.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void Blank_options_never_assume_host_product_or_feature_names()
    {
        var options = new ShortLinkSubscriptionOptions();

        options.ProductCode.ShouldBeEmpty();
        options.QuotaFeatureKey.ShouldBeEmpty();
        options.StatisticsFeatureKey.ShouldBeEmpty();
        new ShortLinkSubscriptionOptionsValidator(Substitute.For<ISubscriptionDefinitionRegistry>())
            .Validate(null, options).Failures.ShouldNotBeNull().Count().ShouldBe(3);
    }

    [Theory]
    [InlineData(nameof(ShortLinkSubscriptionOptions.QuotaFeatureKey))]
    [InlineData(nameof(ShortLinkSubscriptionOptions.StatisticsFeatureKey))]
    public void Mis_typed_feature_mappings_fail_validation(string property)
    {
        var options = BridgeTestDefinitions.Options();
        typeof(ShortLinkSubscriptionOptions).GetProperty(property)!.SetValue(options,
            property == nameof(ShortLinkSubscriptionOptions.QuotaFeatureKey)
                ? BridgeTestDefinitions.StatisticsFeatureKey
                : BridgeTestDefinitions.QuotaFeatureKey);
        var definitions = Definitions();

        var result = new ShortLinkSubscriptionOptionsValidator(definitions).Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(property);
        result.FailureMessage.ShouldContain(property == nameof(ShortLinkSubscriptionOptions.QuotaFeatureKey)
            ? "Numeric"
            : "Boolean");
    }

    [Theory]
    [InlineData(nameof(ShortLinkSubscriptionOptions.ProductCode), SubscriptionErrorCodes.UnknownProduct)]
    [InlineData(nameof(ShortLinkSubscriptionOptions.QuotaFeatureKey), SubscriptionErrorCodes.UnknownFeature)]
    [InlineData(nameof(ShortLinkSubscriptionOptions.StatisticsFeatureKey), SubscriptionErrorCodes.UnknownFeature)]
    public void Unknown_products_or_features_are_explicit_errors(string property, string code)
    {
        var options = BridgeTestDefinitions.Options();
        typeof(ShortLinkSubscriptionOptions).GetProperty(property)!.SetValue(options, "unknown");

        var error = Should.Throw<BusinessException>(() =>
            new ShortLinkSubscriptionOptionsValidator(Definitions()).Validate(null, options));

        error.Code.ShouldBe(code);
    }

    [Fact]
    public void Correct_types_and_normalized_mapping_keys_are_accepted()
    {
        var options = BridgeTestDefinitions.Options();
        options.ProductCode = " TEST-PRODUCT ";
        options.QuotaFeatureKey = " CAPACITY ";
        options.StatisticsFeatureKey = " REPORTS ";

        new ShortLinkSubscriptionOptionsValidator(Definitions())
            .Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Module_checks_options_at_initialization_not_during_service_configuration()
    {
        var definitions = Definitions();
        var services = new ServiceCollection();
        services.AddSingleton(definitions);
        var module = new ShortLinkSubscriptionDomainModule();

        module.ConfigureServices(new ServiceConfigurationContext(services));
        definitions.ReceivedCalls().ShouldBeEmpty();
        using var provider = services.BuildServiceProvider();

        var error = Should.Throw<OptionsValidationException>(() =>
            module.OnApplicationInitialization(new ApplicationInitializationContext(provider)));

        error.Failures.Count().ShouldBe(3);
        definitions.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void Host_configuration_after_bridge_configuration_is_validated_at_initialization()
    {
        var definitions = Definitions();
        var services = new ServiceCollection();
        services.AddSingleton(definitions);
        var module = new ShortLinkSubscriptionDomainModule();
        module.ConfigureServices(new ServiceConfigurationContext(services));
        services.Configure<ShortLinkSubscriptionOptions>(options =>
        {
            options.ProductCode = BridgeTestDefinitions.ProductCode;
            options.QuotaFeatureKey = BridgeTestDefinitions.QuotaFeatureKey;
            options.StatisticsFeatureKey = BridgeTestDefinitions.StatisticsFeatureKey;
        });
        using var provider = services.BuildServiceProvider();

        module.OnApplicationInitialization(new ApplicationInitializationContext(provider));

        definitions.Received(1).GetProduct(BridgeTestDefinitions.ProductCode);
    }

    [Fact]
    public void Wrong_types_fail_initialization_even_without_any_capability_request()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Definitions());
        var module = new ShortLinkSubscriptionDomainModule();
        module.ConfigureServices(new ServiceConfigurationContext(services));
        services.Configure<ShortLinkSubscriptionOptions>(options =>
        {
            options.ProductCode = BridgeTestDefinitions.ProductCode;
            options.QuotaFeatureKey = BridgeTestDefinitions.StatisticsFeatureKey;
            options.StatisticsFeatureKey = BridgeTestDefinitions.QuotaFeatureKey;
        });
        using var provider = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() =>
            module.OnApplicationInitialization(new ApplicationInitializationContext(provider)))
            .Failures.Count().ShouldBe(2);
    }

    private static ISubscriptionDefinitionRegistry Definitions()
    {
        var product = BridgeTestDefinitions.Product();
        var definitions = Substitute.For<ISubscriptionDefinitionRegistry>();
        definitions.GetProduct(Arg.Any<string>()).Returns(call =>
            SubscriptionCode.Normalize(call.Arg<string>()) == product.Code
                ? product
                : throw new BusinessException(SubscriptionErrorCodes.UnknownProduct));
        definitions.ClearReceivedCalls();
        return definitions;
    }
}
