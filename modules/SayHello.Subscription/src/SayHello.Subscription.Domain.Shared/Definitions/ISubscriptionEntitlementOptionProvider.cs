using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;

namespace SayHello.Subscription.Definitions;

/// <summary>
/// Supplies the current business-owned values for enum and string-set entitlements.
/// CanProvide declares ownership even when the current option list is empty.
/// An empty string-set option list enables free-form input; enum options must never be empty.
/// </summary>
public interface ISubscriptionEntitlementOptionProvider
{
    bool CanProvide(string productCode, string featureKey);

    Task<IReadOnlyList<string>> GetOptionsAsync(
        string productCode,
        string featureKey,
        CancellationToken cancellationToken = default);
}

public sealed class NullSubscriptionEntitlementOptionProvider : ISubscriptionEntitlementOptionProvider
{
    public bool CanProvide(string productCode, string featureKey) => false;

    public Task<IReadOnlyList<string>> GetOptionsAsync(
        string productCode,
        string featureKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}

public static class SubscriptionEntitlementOptionProviderExtensions
{
    public static Task<IReadOnlyList<string>> GetCanonicalOptionsAsync(
        this ISubscriptionEntitlementOptionProvider provider,
        string productCode,
        FeatureDefinition feature,
        CancellationToken cancellationToken = default) =>
        new[] { provider }.GetCanonicalOptionsAsync(
            productCode,
            feature,
            cancellationToken);

    public static async Task<IReadOnlyList<string>> GetCanonicalOptionsAsync(
        this IEnumerable<ISubscriptionEntitlementOptionProvider> providers,
        string productCode,
        FeatureDefinition feature,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(feature);
        var normalizedProductCode = SubscriptionCode.Normalize(productCode);
        if (feature.Type is not (SubscriptionEntitlementType.Enum or SubscriptionEntitlementType.StringSet))
        {
            return Array.Empty<string>();
        }

        var matchingProviders = providers
            .Where(provider => provider.CanProvide(normalizedProductCode, feature.Key))
            .Take(2)
            .ToArray();
        if (matchingProviders.Length > 1)
        {
            throw new BusinessException(SubscriptionErrorCodes.EntitlementOptionProviderConflict)
                .WithData("ProductCode", normalizedProductCode)
                .WithData("FeatureKey", feature.Key);
        }

        var supplied = matchingProviders.Length == 0
            ? Array.Empty<string>()
            : await matchingProviders[0].GetOptionsAsync(
                normalizedProductCode,
                feature.Key,
                cancellationToken);
        IReadOnlyList<string> options;
        try
        {
            if (supplied is null)
            {
                throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
            }

            var canonical = new List<string>(supplied.Count);
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in supplied)
            {
                var normalized = EntitlementValue.Enum(item).StringValue!;
                if (!distinct.Add(normalized))
                {
                    throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
                }

                canonical.Add(normalized);
            }

            canonical.Sort(StringComparer.Ordinal);
            options = canonical;
        }
        catch (BusinessException)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementOptions)
                .WithData("ProductCode", normalizedProductCode)
                .WithData("FeatureKey", feature.Key);
        }

        if (feature.Type == SubscriptionEntitlementType.Enum && options.Count == 0)
        {
            throw new BusinessException(SubscriptionErrorCodes.EntitlementOptionsRequired)
                .WithData("ProductCode", normalizedProductCode)
                .WithData("FeatureKey", feature.Key);
        }

        return options;
    }

    public static async Task ValidateOptionsAsync(
        this ISubscriptionEntitlementOptionProvider provider,
        string productCode,
        FeatureDefinition feature,
        EntitlementValue value,
        CancellationToken cancellationToken = default) =>
        await new[] { provider }.ValidateOptionsAsync(
            productCode,
            feature,
            value,
            cancellationToken);

    public static async Task ValidateOptionsAsync(
        this IEnumerable<ISubscriptionEntitlementOptionProvider> providers,
        string productCode,
        FeatureDefinition feature,
        EntitlementValue value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        feature.Validate(value);
        if (feature.Type is not (SubscriptionEntitlementType.Enum or SubscriptionEntitlementType.StringSet))
        {
            return;
        }

        var options = await providers.GetCanonicalOptionsAsync(
            productCode,
            feature,
            cancellationToken);
        var valid = feature.Type == SubscriptionEntitlementType.Enum
            ? options.Contains(value.StringValue!, StringComparer.Ordinal)
            : options.Count == 0 || value.StringValues!.All(item => options.Contains(item, StringComparer.Ordinal));
        if (!valid)
        {
            throw new BusinessException(SubscriptionErrorCodes.EntitlementOptionNotAllowed)
                .WithData("ProductCode", SubscriptionCode.Normalize(productCode))
                .WithData("FeatureKey", feature.Key);
        }
    }
}
