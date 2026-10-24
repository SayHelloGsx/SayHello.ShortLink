using System;
using System.Collections.Generic;
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

/// <summary>
/// Default <see cref="IShortLinkCreationRateLimiter"/> that counts link creations per tenant and user
/// in fixed one-hour windows held in the memory of the current process.
/// </summary>
/// <remarks>
/// Counters are neither shared between application instances nor kept across restarts, so this
/// implementation only enforces the limit for a single-instance host or in tests. A host that runs
/// more than one instance must replace <see cref="IShortLinkCreationRateLimiter"/> with an
/// implementation backed by shared storage, as the Web Host does with Redis. Expired windows are
/// evicted at most once per window length, which bounds memory by the users that created links
/// during the last two windows.
/// </remarks>
public class InMemoryShortLinkCreationRateLimiter :
    IShortLinkCreationRateLimiter,
    ISingletonDependency
{
    private static readonly TimeSpan WindowLength = TimeSpan.FromHours(1);

    private readonly Dictionary<string, WindowState> _windows = new(StringComparer.Ordinal);
    private readonly Lock _syncRoot = new();
    private readonly ISettingProvider _settingProvider;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;
    private DateTime _nextPruneAt = DateTime.MinValue;

    public InMemoryShortLinkCreationRateLimiter(
        ISettingProvider settingProvider,
        IClock clock,
        ICurrentTenant currentTenant)
    {
        _settingProvider = settingProvider;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    internal int TrackedWindowCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _windows.Count;
            }
        }
    }

    public virtual async Task EnsureAllowedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var limit = await GetLimitAsync();
        var tenantId = _currentTenant.Id;
        var key = $"{tenantId?.ToString("N") ?? "host"}:{userId:N}";

        lock (_syncRoot)
        {
            var now = _clock.Now;
            PruneExpiredWindows(now);

            if (!_windows.TryGetValue(key, out var state) || IsExpired(state, now))
            {
                state = new WindowState(now);
                _windows[key] = state;
            }

            if (state.Count >= limit)
            {
                throw new BusinessException(ShortLinkErrorCodes.CreationRateExceeded)
                    .WithData("Limit", limit);
            }

            state.Count++;
        }
    }

    private void PruneExpiredWindows(DateTime now)
    {
        if (now < _nextPruneAt)
        {
            return;
        }

        var expiredKeys = new List<string>();
        foreach (var (key, state) in _windows)
        {
            if (IsExpired(state, now))
            {
                expiredKeys.Add(key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _windows.Remove(key);
        }

        _nextPruneAt = now + WindowLength;
    }

    private static bool IsExpired(WindowState state, DateTime now)
    {
        return now - state.StartedAt >= WindowLength;
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

        public DateTime StartedAt { get; }

        public int Count { get; set; }
    }
}
