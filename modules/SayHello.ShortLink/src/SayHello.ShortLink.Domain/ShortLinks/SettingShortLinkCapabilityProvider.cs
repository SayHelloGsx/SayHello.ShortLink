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

    public async Task<ShortLinkQuota> GetQuotaAsync(
        Guid? tenantId,
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

    public Task<bool> IsStatisticsEnabledAsync(
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
