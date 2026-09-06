using System;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.ShortLink.WebHost.Data;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using Volo.Abp.Data;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace SayHello.ShortLink.WebHost.Subscriptions;

public class SubscriptionProductDataSeedContributorTests
{
    [Fact]
    public async Task Seed_Should_Create_Only_Missing_Product_Metadata()
    {
        var products = Substitute.For<ISubscriptionProductRepository>();
        var catalog = Substitute.For<ISubscriptionCatalogManager>();
        var tenant = Substitute.For<ICurrentTenant>();
        var contributor = new SubscriptionProductDataSeedContributor(products, catalog, tenant);

        await contributor.SeedAsync(new DataSeedContext());

        await catalog.Received(1).CreateProductAsync(
            null,
            ShortLinkSubscriptionDefinitions.ProductCode,
            Arg.Is<CatalogDetails>(details => details.Name == "ShortLink"));
    }

    [Fact]
    public async Task Seed_Should_Not_Overwrite_Existing_Product()
    {
        var products = Substitute.For<ISubscriptionProductRepository>();
        var catalog = Substitute.For<ISubscriptionCatalogManager>();
        var tenant = Substitute.For<ICurrentTenant>();
        var tenantId = Guid.NewGuid();
        var product = new SubscriptionProduct(
            Guid.NewGuid(), tenantId,
            new ProductDefinition(ShortLinkSubscriptionDefinitions.ProductCode, new FixedLocalizableString("ShortLink")),
            "Customized catalog name");
        products.FindByCodeAsync(tenantId, ShortLinkSubscriptionDefinitions.ProductCode).Returns(product);

        await new SubscriptionProductDataSeedContributor(products, catalog, tenant)
            .SeedAsync(new DataSeedContext(tenantId));

        await catalog.DidNotReceiveWithAnyArgs().CreateProductAsync(default, default!, default!);
        tenant.Received(1).Change(tenantId);
    }
}
