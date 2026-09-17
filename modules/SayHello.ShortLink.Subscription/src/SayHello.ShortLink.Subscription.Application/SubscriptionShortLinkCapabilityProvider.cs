using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinks;
using SayHello.Subscription.Public.Entitlements;
using Volo.Abp.Authorization;
using Volo.Abp.Users;

namespace SayHello.ShortLink.Subscription;

public sealed class SubscriptionShortLinkCapabilityProvider : IShortLinkCapabilityProvider
{
    private readonly ICurrentUserEntitlementAppService _entitlements;
    private readonly ICurrentUser _currentUser;

    public SubscriptionShortLinkCapabilityProvider(
        ICurrentUserEntitlementAppService entitlements,
        ICurrentUser currentUser)
    {
        _entitlements = entitlements;
        _currentUser = currentUser;
    }

    public bool IsQuotaExternallyManaged => true;

    public async Task<ShortLinkQuota> GetQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrentSubject(userId);
        var result = await _entitlements.GetNumericAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.MaxLinks,
            cancellationToken);
        if (!result.IsGranted)
        {
            return ShortLinkQuota.Denied;
        }

        return result.IsUnlimited
            ? ShortLinkQuota.Unlimited
            : ShortLinkQuota.Limited(result.Limit ??
                throw new InvalidOperationException("A granted finite subscription quota must have a limit."));
    }

    public async Task<ShortLinkStatisticsLevel> GetStatisticsLevelAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrentSubject(userId);
        var result = await _entitlements.GetEnumAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.Statistics,
            cancellationToken);
        if (!result.IsGranted)
        {
            return ShortLinkStatisticsLevel.None;
        }

        return result.Value switch
        {
            ShortLinkSubscriptionDefinitions.StatisticsNone => ShortLinkStatisticsLevel.None,
            ShortLinkSubscriptionDefinitions.StatisticsBasic => ShortLinkStatisticsLevel.Basic,
            ShortLinkSubscriptionDefinitions.StatisticsAdvanced => ShortLinkStatisticsLevel.Advanced,
            _ => throw new InvalidOperationException(
                $"Unknown short-link statistics entitlement value '{result.Value}'.")
        };
    }

    public async Task<ShortLinkDomainAccess> GetDomainAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrentSubject(userId);
        var result = await _entitlements.GetStringSetAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.Domains,
            cancellationToken);
        if (!result.IsGranted)
        {
            return ShortLinkDomainAccess.Restricted(Array.Empty<string>());
        }

        return ShortLinkDomainAccess.Restricted(
            result.Values.Select(ShortLinkDomainOrigin.Normalize));
    }

    private void EnsureCurrentSubject(Guid userId)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.Id != userId)
        {
            throw new AbpAuthorizationException();
        }
    }
}
