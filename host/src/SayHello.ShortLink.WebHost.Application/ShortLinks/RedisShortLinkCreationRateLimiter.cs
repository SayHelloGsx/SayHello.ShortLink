using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace SayHello.ShortLink.WebHost.ShortLinks;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IShortLinkCreationRateLimiter))]
public class RedisShortLinkCreationRateLimiter :
    IShortLinkCreationRateLimiter,
    ITransientDependency
{
    private const string IncrementScript = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;

    public RedisShortLinkCreationRateLimiter(
        IConnectionMultiplexer connectionMultiplexer,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
    }

    public async Task EnsureAllowedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var limit = await GetLimitAsync();
        var tenantId = _currentTenant.Id;
        var key = new RedisKey(
            $"short-link:create-rate:{tenantId?.ToString("N") ?? "host"}:{userId:N}");
        var database = _connectionMultiplexer.GetDatabase();
        var result = await database.ScriptEvaluateAsync(
                IncrementScript,
                [key],
                [3600])
            .WaitAsync(cancellationToken);

        if ((long)result > limit)
        {
            throw new BusinessException(ShortLinkErrorCodes.CreationRateExceeded)
                .WithData("Limit", limit);
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
}
