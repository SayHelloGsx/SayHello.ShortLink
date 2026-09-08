using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.ShortLinks;
using SayHello.Subscription.Entitlements;

namespace SayHello.ShortLink.Subscription;

public sealed class SubscriptionShortLinkCapabilityProvider : IShortLinkCapabilityProvider
{
    private readonly ISubscriptionEntitlementChecker _entitlements;
    private readonly ShortLinkSubscriptionOptions _options;

    public SubscriptionShortLinkCapabilityProvider(
        ISubscriptionEntitlementChecker entitlements,
        IOptions<ShortLinkSubscriptionOptions> options)
    {
        _entitlements = entitlements;
        _options = options.Value;
    }

    public bool IsQuotaExternallyManaged => true;

    public async Task<ShortLinkQuota> GetQuotaAsync(
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _entitlements.GetNumericAsync(
            tenantId, userId, _options.ProductCode, _options.QuotaFeatureKey, cancellationToken);
        if (!result.IsGranted)
        {
            return ShortLinkQuota.Denied;
        }

        return result.IsUnlimited
            ? ShortLinkQuota.Unlimited
            : ShortLinkQuota.Limited(result.Limit ??
                throw new InvalidOperationException("A granted finite subscription quota must have a limit."));
    }

    public async Task<bool> IsStatisticsEnabledAsync(
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _entitlements.GetBooleanAsync(
            tenantId, userId, _options.ProductCode, _options.StatisticsFeatureKey, cancellationToken);
        return result.IsGranted;
    }
}
