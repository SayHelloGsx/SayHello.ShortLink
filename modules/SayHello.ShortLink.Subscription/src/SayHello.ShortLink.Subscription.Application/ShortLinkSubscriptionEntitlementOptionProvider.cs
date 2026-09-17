using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.Subscription.Definitions;

namespace SayHello.ShortLink.Subscription;

public sealed class ShortLinkSubscriptionEntitlementOptionProvider :
    ISubscriptionEntitlementOptionProvider
{
    private static readonly IReadOnlyList<string> StatisticsOptions =
    [
        ShortLinkSubscriptionDefinitions.StatisticsNone,
        ShortLinkSubscriptionDefinitions.StatisticsBasic,
        ShortLinkSubscriptionDefinitions.StatisticsAdvanced
    ];

    private readonly IShortLinkDomainRepository _domains;

    public ShortLinkSubscriptionEntitlementOptionProvider(
        IShortLinkDomainRepository domains)
    {
        _domains = domains;
    }

    public bool CanProvide(string productCode, string featureKey) =>
        string.Equals(
            productCode,
            ShortLinkSubscriptionDefinitions.ProductCode,
            StringComparison.Ordinal) &&
        featureKey is
            ShortLinkSubscriptionDefinitions.Statistics or
            ShortLinkSubscriptionDefinitions.Domains;

    public async Task<IReadOnlyList<string>> GetOptionsAsync(
        string productCode,
        string featureKey,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                productCode,
                ShortLinkSubscriptionDefinitions.ProductCode,
                StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        if (string.Equals(
                featureKey,
                ShortLinkSubscriptionDefinitions.Statistics,
                StringComparison.Ordinal))
        {
            return StatisticsOptions;
        }

        if (string.Equals(
                featureKey,
                ShortLinkSubscriptionDefinitions.Domains,
                StringComparison.Ordinal))
        {
            return (await _domains.GetListAsync(cancellationToken))
                .Where(domain => domain.IsEnabled)
                .Select(domain => domain.Origin)
                .OrderBy(origin => origin, StringComparer.Ordinal)
                .ToArray();
        }

        return Array.Empty<string>();
    }
}
