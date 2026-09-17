using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Timing;

namespace SayHello.ShortLink.Public.ShortLinks;

public class InMemoryShortLinkCreationRateLimiter :
    IShortLinkCreationRateLimiter,
    ISingletonDependency
{
    private readonly ConcurrentDictionary<string, WindowState> _windows = new();
    private readonly ISettingProvider _settingProvider;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;

    public InMemoryShortLinkCreationRateLimiter(
        ISettingProvider settingProvider,
        IClock clock,
        ICurrentTenant currentTenant)
    {
        _settingProvider = settingProvider;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    public async Task EnsureAllowedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var limit = await GetLimitAsync();
        var tenantId = _currentTenant.Id;
        var key = $"{tenantId?.ToString("N") ?? "host"}:{userId:N}";
        var state = _windows.GetOrAdd(key, _ => new WindowState(_clock.Now));

        lock (state)
        {
            var now = _clock.Now;
            if (now - state.StartedAt >= TimeSpan.FromHours(1))
            {
                state.StartedAt = now;
                state.Count = 0;
            }

            if (state.Count >= limit)
            {
                throw new BusinessException(ShortLinkErrorCodes.CreationRateExceeded)
                    .WithData("Limit", limit);
            }

            state.Count++;
        }
    }

    private async Task<int> GetLimitAsync()
    {
        var value = await _settingProvider.GetOrNullAsync(ShortLinkSettings.CreateLimitPerHour);
        if (value is null)
        {
            return ShortLinkDefaults.CreateLimitPerHour;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) ||
            limit <= 0)
        {
            throw new AbpException(
                $"Setting '{ShortLinkSettings.CreateLimitPerHour}' must be a positive integer.");
        }

        return limit;
    }

    private sealed class WindowState
    {
        public WindowState(DateTime startedAt)
        {
            StartedAt = startedAt;
        }

        public DateTime StartedAt { get; set; }

        public int Count { get; set; }
    }
}
