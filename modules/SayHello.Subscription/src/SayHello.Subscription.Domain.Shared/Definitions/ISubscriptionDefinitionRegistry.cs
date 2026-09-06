using System.Collections.Generic;

namespace SayHello.Subscription.Definitions;

/// <summary>
/// Resolves the configured providers once into an immutable registry. Duplicate normalized product
/// codes are configuration errors; unknown product or feature lookups are explicit business errors.
/// </summary>
public interface ISubscriptionDefinitionRegistry
{
    IReadOnlyList<ProductDefinition> GetProducts();
    ProductDefinition GetProduct(string productCode);
    FeatureDefinition GetFeature(string productCode, string featureKey);
}
