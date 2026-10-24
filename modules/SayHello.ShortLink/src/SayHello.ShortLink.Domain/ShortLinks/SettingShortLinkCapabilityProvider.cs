using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.Settings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace SayHello.ShortLink.ShortLinks;

public class SettingShortLinkCapabilityProvider : IShortLinkCapabilityProvider, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;

    public bool IsQuotaExternallyManaged => false;

    public SettingShortLinkCapabilityProvider(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public virtual async Task<ShortLinkQuota> GetQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _settingProvider.GetOrNullAsync(ShortLinkSettings.MaxLinksPerUser);
        if (value is null)
        {
            return ShortLinkQuota.Limited(ShortLinkDefaults.MaxLinksPerUser);
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) ||
            limit <= 0)
        {
            throw new AbpException($"Setting '{ShortLinkSettings.MaxLinksPerUser}' must be a positive integer.");
        }

        return ShortLinkQuota.Limited(limit);
    }

    public virtual Task<ShortLinkQuota> GetCustomCodeQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ShortLinkQuota.Unlimited);
    }

    public virtual Task<ShortLinkQuota> GetNamespaceQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ShortLinkQuota.Unlimited);
    }

    public virtual Task<ShortLinkQuota> GetRootLinkQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ShortLinkQuota.Unlimited);
    }

    public virtual Task<ShortLinkStatisticsLevel> GetStatisticsLevelAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ShortLinkStatisticsLevel.Advanced);
    }
    public virtual Task<ShortLinkDomainAccess> GetDomainAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ShortLinkDomainAccess.Unrestricted);
    }
}
