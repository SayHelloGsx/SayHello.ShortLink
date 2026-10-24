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
    public void Namespace_and_root_feature_keys_are_stable()
    {
        ShortLinkSubscriptionDefinitions.MaxNamespaces.ShouldBe("max-namespaces");
        ShortLinkSubscriptionDefinitions.MaxRootLinks.ShouldBe("max-root-links");
    }

    [Fact]
    public void Definition_has_the_expected_typed_features()
    {
        var context = new DefinitionContext();

        new ShortLinkSubscriptionDefinitionProvider().Define(context);

        var product = context.Products.ShouldHaveSingleItem();
        product.Code.ShouldBe(ShortLinkSubscriptionDefinitions.ProductCode);
        var statistics = product.GetFeature(ShortLinkSubscriptionDefinitions.Statistics);
        statistics.Type.ShouldBe(SubscriptionEntitlementType.Enum);
        statistics.AllowUnlimited.ShouldBeFalse();
        var domains = product.GetFeature(ShortLinkSubscriptionDefinitions.Domains);
        domains.Type.ShouldBe(SubscriptionEntitlementType.StringSet);
        domains.AllowUnlimited.ShouldBeFalse();
        var limit = product.GetFeature(ShortLinkSubscriptionDefinitions.MaxLinks);
        limit.Type.ShouldBe(SubscriptionEntitlementType.Numeric);
        limit.AllowUnlimited.ShouldBeTrue();
        limit.Validate(EntitlementValue.Numeric(0));
        limit.Validate(EntitlementValue.Unlimited());
        var customCodeLimit = product.GetFeature(
            ShortLinkSubscriptionDefinitions.MaxCustomCodes);
        customCodeLimit.Type.ShouldBe(SubscriptionEntitlementType.Numeric);
        customCodeLimit.AllowUnlimited.ShouldBeTrue();
        customCodeLimit.Validate(EntitlementValue.Numeric(0));
        customCodeLimit.Validate(EntitlementValue.Unlimited());
        product.Features.ContainsKey("namespaces").ShouldBeFalse();
        Should.Throw<BusinessException>(() => product.GetFeature("namespaces"))
            .Code.ShouldBe(SubscriptionErrorCodes.UnknownFeature);
        foreach (var featureKey in new[]
                 {
                     ShortLinkSubscriptionDefinitions.MaxNamespaces,
                     ShortLinkSubscriptionDefinitions.MaxRootLinks
                 })
        {
            var quota = product.GetFeature(featureKey);
            quota.Type.ShouldBe(SubscriptionEntitlementType.Numeric);
            quota.AllowUnlimited.ShouldBeTrue();
            quota.Maximum.ShouldBeNull();
            quota.Validate(EntitlementValue.Numeric(0));
            quota.Validate(EntitlementValue.Numeric(20));
            quota.Validate(EntitlementValue.Numeric(long.MaxValue));
            quota.Validate(EntitlementValue.Unlimited());
            Should.Throw<BusinessException>(() => quota.Validate(EntitlementValue.Boolean(true)));
        }
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

            product.Features.Count.ShouldBe(6);
        }
        finally
        {
            await application.ShutdownAsync();
        }
    }

    [Theory]
    [InlineData("en", "ShortLink", "Visit statistics level", "Additional short-link domains",
        "Maximum number of links", "Maximum number of custom codes",
        "Maximum number of namespaces", "Maximum number of root links")]
    [InlineData("zh-Hans", "短链接", "访问统计等级", "额外短链接域名", "链接数量上限",
        "自定义短码数量上限", "命名空间数量上限", "根链接数量上限")]
    public void Bridge_owns_matching_localized_definition_texts(
        string culture,
        string product,
        string statistics,
        string domains,
        string maxLinks,
        string maxCustomCodes,
        string maxNamespaces,
        string maxRootLinks)
    {
        var assembly = typeof(ShortLinkSubscriptionResource).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith($"Localization.ShortLinkSubscription.{culture}.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var json = JsonDocument.Parse(stream);
        var texts = json.RootElement.GetProperty("texts");

        texts.GetProperty("Subscription:ShortLink").GetString().ShouldBe(product);
        texts.GetProperty("Subscription:Statistics").GetString().ShouldBe(statistics);
        texts.GetProperty("Subscription:Domains").GetString().ShouldBe(domains);
        texts.GetProperty("Subscription:MaxLinks").GetString().ShouldBe(maxLinks);
        texts.GetProperty("Subscription:MaxCustomCodes").GetString()
            .ShouldBe(maxCustomCodes);
        texts.TryGetProperty("Subscription:Namespaces", out _).ShouldBeFalse();
        texts.GetProperty("Subscription:MaxNamespaces").GetString().ShouldBe(maxNamespaces);
        texts.GetProperty("Subscription:MaxRootLinks").GetString().ShouldBe(maxRootLinks);
    }

    private sealed class DefinitionContext : ISubscriptionDefinitionContext
    {
        public List<ProductDefinition> Products { get; } = [];

        public void AddProduct(ProductDefinition product) => Products.Add(product);
    }
}

[DependsOn(typeof(ShortLinkSubscriptionDomainSharedModule), typeof(AbpAutofacModule))]
public class DefinitionTestModule : AbpModule;
