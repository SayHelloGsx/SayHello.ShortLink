using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Subscription.Localization;
using SayHello.Subscription;
using SayHello.Subscription.Definitions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class ShortLinkSubscriptionDefinitionTests
{
    [Fact]
    public void Definition_has_the_expected_typed_features()
    {
        var context = new DefinitionContext();

        new ShortLinkSubscriptionDefinitionProvider().Define(context);

        var product = context.Products.ShouldHaveSingleItem();
        product.Code.ShouldBe(ShortLinkSubscriptionDefinitions.ProductCode);
        var statistics = product.GetFeature(ShortLinkSubscriptionDefinitions.Statistics);
        statistics.Type.ShouldBe(SubscriptionEntitlementType.Boolean);
        statistics.AllowUnlimited.ShouldBeFalse();
        var limit = product.GetFeature(ShortLinkSubscriptionDefinitions.MaxLinks);
        limit.Type.ShouldBe(SubscriptionEntitlementType.Numeric);
        limit.AllowUnlimited.ShouldBeTrue();
        limit.Validate(EntitlementValue.Numeric(0));
        limit.Validate(EntitlementValue.Unlimited());
    }

    [Fact]
    public async Task Shared_module_registers_the_definition_without_a_host()
    {
        using var application = await AbpApplicationFactory.CreateAsync<DefinitionTestModule>(
            options => options.UseAutofac());
        await application.InitializeAsync();
        try
        {
            var product = application.ServiceProvider.GetRequiredService<ISubscriptionDefinitionRegistry>()
                .GetProduct(ShortLinkSubscriptionDefinitions.ProductCode);

            product.Features.Count.ShouldBe(2);
        }
        finally
        {
            await application.ShutdownAsync();
        }
    }

    [Theory]
    [InlineData("en", "ShortLink", "Visit statistics", "Maximum number of links")]
    [InlineData("zh-Hans", "短链接", "访问统计", "链接数量上限")]
    public void Bridge_owns_matching_localized_definition_texts(
        string culture,
        string product,
        string statistics,
        string maxLinks)
    {
        var assembly = typeof(ShortLinkSubscriptionResource).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith($"Localization.ShortLinkSubscription.{culture}.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var json = JsonDocument.Parse(stream);
        var texts = json.RootElement.GetProperty("texts");

        texts.GetProperty("Subscription:ShortLink").GetString().ShouldBe(product);
        texts.GetProperty("Subscription:Statistics").GetString().ShouldBe(statistics);
        texts.GetProperty("Subscription:MaxLinks").GetString().ShouldBe(maxLinks);
    }

    private sealed class DefinitionContext : ISubscriptionDefinitionContext
    {
        public List<ProductDefinition> Products { get; } = [];

        public void AddProduct(ProductDefinition product) => Products.Add(product);
    }
}

[DependsOn(typeof(ShortLinkSubscriptionDomainSharedModule), typeof(AbpAutofacModule))]
public class DefinitionTestModule : AbpModule;
