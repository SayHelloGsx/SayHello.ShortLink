using System;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinks;
using SayHello.Subscription.Public.Entitlements;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace SayHello.ShortLink.Subscription;

public sealed class SubscriptionShortLinkCapabilityProvider : IShortLinkCapabilityProvider
{
    private readonly ICurrentUserEntitlementAppService _entitlements;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public SubscriptionShortLinkCapabilityProvider(
        ICurrentUserEntitlementAppService entitlements,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant)
    {
        _entitlements = entitlements;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    public bool IsQuotaExternallyManaged => true;

    public async Task<ShortLinkQuota> GetQuotaAsync(
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrentSubject(tenantId, userId);
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

    public async Task<bool> IsStatisticsEnabledAsync(
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrentSubject(tenantId, userId);
        var result = await _entitlements.GetBooleanAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.Statistics,
            cancellationToken);
        return result.IsGranted;
    }

    private void EnsureCurrentSubject(Guid? tenantId, Guid userId)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.Id != userId ||
            _currentTenant.Id != tenantId)
        {
            throw new AbpAuthorizationException();
        }
    }
}
