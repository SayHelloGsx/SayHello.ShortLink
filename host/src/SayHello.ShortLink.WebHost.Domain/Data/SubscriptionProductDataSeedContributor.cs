using System.Threading.Tasks;
using SayHello.ShortLink.WebHost.Subscriptions;
using SayHello.Subscription.Catalog;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.WebHost.Data;

public class SubscriptionProductDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ISubscriptionProductRepository _products;
    private readonly ISubscriptionCatalogManager _catalog;
    private readonly ICurrentTenant _currentTenant;

    public SubscriptionProductDataSeedContributor(
        ISubscriptionProductRepository products,
        ISubscriptionCatalogManager catalog,
        ICurrentTenant currentTenant)
    {
        _products = products;
        _catalog = catalog;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        using (_currentTenant.Change(context.TenantId))
        {
            var product = await _products.FindByCodeAsync(
                context.TenantId, ShortLinkSubscriptionDefinitions.ProductCode);
            if (product is null)
            {
                await _catalog.CreateProductAsync(
                    context.TenantId,
                    ShortLinkSubscriptionDefinitions.ProductCode,
                    new CatalogDetails("ShortLink"));
            }
        }
    }
}
