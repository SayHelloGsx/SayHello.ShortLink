using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;

namespace SayHello.ShortLink.Common.BlockedDomains;

public class BlockedDomainCache : IBlockedDomainCache, ITransientDependency
{
    private static readonly TimeSpan ResolutionDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IndexDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private readonly IBlockedDomainRepository _repository;
    private readonly IDistributedCache<BlockedDomainResolutionCacheItem, string> _resolutionCache;
    private readonly IDistributedCache<BlockedDomainHostIndexCacheItem, string> _hostIndexCache;
    private readonly IAbpDistributedLock _distributedLock;

    public BlockedDomainCache(
        IBlockedDomainRepository repository,
        IDistributedCache<BlockedDomainResolutionCacheItem, string> resolutionCache,
        IDistributedCache<BlockedDomainHostIndexCacheItem, string> hostIndexCache,
        IAbpDistributedLock distributedLock)
    {
        _repository = repository;
        _resolutionCache = resolutionCache;
        _hostIndexCache = hostIndexCache;
        _distributedLock = distributedLock;
    }

    public async Task<BlockedDomainResolutionCacheItem> GetAsync(
        string host,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var normalizedHost = DomainNameNormalizer.Normalize(host);
        var cacheKey = GetResolutionKey(normalizedHost, tenantId);
        var cached = await _resolutionCache.GetAsync(cacheKey, token: cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        await using var handle = await _distributedLock.TryAcquireAsync(
            GetLockKey(tenantId),
            LockTimeout,
            cancellationToken);
        if (handle is null)
        {
            return await ResolveAsync(normalizedHost, tenantId, cancellationToken);
        }

        cached = await _resolutionCache.GetAsync(cacheKey, token: cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await ResolveAsync(normalizedHost, tenantId, cancellationToken);
        await AddHostToIndexAsync(normalizedHost, tenantId, cancellationToken);
        await _resolutionCache.SetAsync(
            cacheKey,
            result,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ResolutionDuration
            },
            token: cancellationToken);
        return result;
    }

    public Task InvalidateAsync(
        string domain,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<string> domains = [domain];
        return InvalidateManyAsync(domains, tenantId, cancellationToken);
    }

    public async Task InvalidateManyAsync(
        IReadOnlyCollection<string> domains,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (domains.Count == 0)
        {
            return;
        }

        var normalizedDomains = domains
            .Select(DomainNameNormalizer.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using var handle = await _distributedLock.TryAcquireAsync(
            GetLockKey(tenantId),
            LockTimeout,
            cancellationToken);
        if (handle is null)
        {
            throw new BusinessException(ShortLinkErrorCodes.BlockedDomainCacheLockTimeout);
        }

        var indexKey = GetIndexKey(tenantId);
        var index = await _hostIndexCache.GetAsync(indexKey, token: cancellationToken) ??
                    new BlockedDomainHostIndexCacheItem();
        var affectedHosts = index.Hosts
            .Where(host => DomainNameNormalizer
                .GetParentCandidates(host)
                .Any(normalizedDomains.Contains))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var host in affectedHosts)
        {
            await _resolutionCache.RemoveAsync(
                GetResolutionKey(host, tenantId),
                token: cancellationToken);
        }

        foreach (var domain in normalizedDomains)
        {
            await _resolutionCache.RemoveAsync(
                GetResolutionKey(domain, tenantId),
                token: cancellationToken);
        }

        if (affectedHosts.Count == 0)
        {
            return;
        }

        var affectedHostSet = affectedHosts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        index.Hosts = index.Hosts
            .Where(host => !affectedHostSet.Contains(host))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (index.Hosts.Count == 0)
        {
            await _hostIndexCache.RemoveAsync(indexKey, token: cancellationToken);
        }
        else
        {
            await SetIndexAsync(indexKey, index, cancellationToken);
        }
    }

    private async Task<BlockedDomainResolutionCacheItem> ResolveAsync(
        string normalizedHost,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var match = await _repository.FindMatchingActiveAsync(
            normalizedHost,
            tenantId,
            cancellationToken);

        return match is null
            ? new BlockedDomainResolutionCacheItem()
            : new BlockedDomainResolutionCacheItem
            {
                IsBlocked = true,
                MatchedDomain = match.Domain,
                Reason = match.Reason
            };
    }

    private async Task AddHostToIndexAsync(
        string normalizedHost,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var indexKey = GetIndexKey(tenantId);
        var index = await _hostIndexCache.GetAsync(indexKey, token: cancellationToken) ??
                    new BlockedDomainHostIndexCacheItem();

        if (!index.Hosts.Contains(normalizedHost, StringComparer.OrdinalIgnoreCase))
        {
            index.Hosts.Add(normalizedHost);
        }

        await SetIndexAsync(indexKey, index, cancellationToken);
    }

    private Task SetIndexAsync(
        string key,
        BlockedDomainHostIndexCacheItem index,
        CancellationToken cancellationToken)
    {
        return _hostIndexCache.SetAsync(
            key,
            index,
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = IndexDuration
            },
            token: cancellationToken);
    }

    private static string GetResolutionKey(string normalizedHost, Guid? tenantId)
    {
        return $"{GetTenantKey(tenantId)}:{normalizedHost}";
    }

    private static string GetIndexKey(Guid? tenantId)
    {
        return GetTenantKey(tenantId);
    }

    private static string GetLockKey(Guid? tenantId)
    {
        return $"BlockedDomainCache:{GetTenantKey(tenantId)}";
    }

    private static string GetTenantKey(Guid? tenantId)
    {
        return tenantId?.ToString("N") ?? "host";
    }
}
