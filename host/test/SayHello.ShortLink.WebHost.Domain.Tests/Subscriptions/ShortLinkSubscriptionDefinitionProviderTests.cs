using System.Collections.Generic;
using SayHello.Subscription;
using SayHello.Subscription.Definitions;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.WebHost.Subscriptions;

public class ShortLinkSubscriptionDefinitionProviderTests
{
    [Fact]
    public void Host_Should_Register_Typed_ShortLink_Entitlements()
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

    private sealed class DefinitionContext : ISubscriptionDefinitionContext
    {
        public List<ProductDefinition> Products { get; } = [];

        public void AddProduct(ProductDefinition product) => Products.Add(product);
    }
}
