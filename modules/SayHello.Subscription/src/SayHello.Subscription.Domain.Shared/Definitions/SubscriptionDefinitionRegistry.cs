using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SayHello.Subscription.Definitions;

public sealed class SubscriptionDefinitionRegistry : ISubscriptionDefinitionRegistry, ISingletonDependency
{
    private readonly Lazy<IReadOnlyDictionary<string, ProductDefinition>> _products;

    public SubscriptionDefinitionRegistry(IServiceScopeFactory scopeFactory, IOptions<SubscriptionDefinitionOptions> options)
    {
        _products = new Lazy<IReadOnlyDictionary<string, ProductDefinition>>(() =>
        {
            using var scope = scopeFactory.CreateScope();
            var context = new DefinitionContext();
            foreach (var providerType in options.Value.DefinitionProviders)
            {
                ((ISubscriptionDefinitionProvider)scope.ServiceProvider.GetRequiredService(providerType)).Define(context);
            }

            return context.Freeze();
        });
    }

    public IReadOnlyList<ProductDefinition> GetProducts() =>
        Array.AsReadOnly(_products.Value.Values.OrderBy(p => p.Code, StringComparer.Ordinal).ToArray());

    public ProductDefinition GetProduct(string productCode)
    {
        var code = SubscriptionCode.Normalize(productCode);
        return _products.Value.TryGetValue(code, out var product)
            ? product
            : throw new BusinessException(SubscriptionErrorCodes.UnknownProduct).WithData("ProductCode", code);
    }

    public FeatureDefinition GetFeature(string productCode, string featureKey) =>
        GetProduct(productCode).GetFeature(featureKey);

    private sealed class DefinitionContext : ISubscriptionDefinitionContext
    {
        private readonly Dictionary<string, ProductDefinition> _products = new(StringComparer.Ordinal);
        private bool _frozen;

        public void AddProduct(ProductDefinition product)
        {
            ArgumentNullException.ThrowIfNull(product);
            if (_frozen || !_products.TryAdd(product.Code, product))
            {
                throw new AbpException($"Duplicate or late subscription product definition: {product.Code}.");
            }
        }

        public IReadOnlyDictionary<string, ProductDefinition> Freeze()
        {
            _frozen = true;
            return new ReadOnlyDictionary<string, ProductDefinition>(_products);
        }
    }
}
