using System.Collections.Generic;
using Microsoft.Extensions.Options;
using SayHello.Subscription;
using SayHello.Subscription.Definitions;

namespace SayHello.ShortLink.Subscription;

public sealed class ShortLinkSubscriptionOptionsValidator : IValidateOptions<ShortLinkSubscriptionOptions>
{
    private readonly ISubscriptionDefinitionRegistry _definitions;

    public ShortLinkSubscriptionOptionsValidator(ISubscriptionDefinitionRegistry definitions)
    {
        _definitions = definitions;
    }

    public ValidateOptionsResult Validate(string? name, ShortLinkSubscriptionOptions options)
    {
        var failures = new List<string>();
        Require(options.ProductCode, nameof(options.ProductCode), failures);
        Require(options.QuotaFeatureKey, nameof(options.QuotaFeatureKey), failures);
        Require(options.StatisticsFeatureKey, nameof(options.StatisticsFeatureKey), failures);
        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        var product = _definitions.GetProduct(options.ProductCode);
        if (product.GetFeature(options.QuotaFeatureKey).Type != SubscriptionEntitlementType.Numeric)
        {
            failures.Add($"{nameof(options.QuotaFeatureKey)} must identify a Numeric subscription feature.");
        }

        if (product.GetFeature(options.StatisticsFeatureKey).Type != SubscriptionEntitlementType.Boolean)
        {
            failures.Add($"{nameof(options.StatisticsFeatureKey)} must identify a Boolean subscription feature.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(string value, string property, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{nameof(ShortLinkSubscriptionOptions)}.{property} is required.");
        }
    }
}
