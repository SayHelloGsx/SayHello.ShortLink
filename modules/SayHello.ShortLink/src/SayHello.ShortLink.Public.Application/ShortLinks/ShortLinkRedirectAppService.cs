using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.Common.BlockedDomains;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.Public.ShortLinks;

[AllowAnonymous]
[RemoteService(false)]
public class ShortLinkRedirectAppService : ShortLinkApplicationService, IShortLinkRedirectAppService
{
    private static readonly TimeSpan FoundCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MissingCacheDuration = TimeSpan.FromSeconds(30);

    private readonly IShortLinkRepository _shortLinkRepository;
    private readonly IDistributedCache<ShortLinkResolutionCacheItem, string> _cache;
    private readonly IVisitorHashService _visitorHashService;
    private readonly IVisitMetadataParser _metadataParser;
    private readonly IBlockedDomainCache _blockedDomainCache;
    private readonly ShortLinkUrlOptions _urlOptions;
    private readonly ILogger<ShortLinkRedirectAppService> _logger;

    public ShortLinkRedirectAppService(
        IShortLinkRepository shortLinkRepository,
        IDistributedCache<ShortLinkResolutionCacheItem, string> cache,
        IVisitorHashService visitorHashService,
        IVisitMetadataParser metadataParser,
        IBlockedDomainCache blockedDomainCache,
        IOptions<ShortLinkUrlOptions> urlOptions,
        ILogger<ShortLinkRedirectAppService> logger)
    {
        _shortLinkRepository = shortLinkRepository;
        _cache = cache;
        _visitorHashService = visitorHashService;
        _metadataParser = metadataParser;
        _blockedDomainCache = blockedDomainCache;
        _urlOptions = urlOptions.Value;
        _logger = logger;
    }

    [UnitOfWork(isTransactional: true)]
    public async Task<ShortLinkResolutionDto> ResolveAsync(
        string origin,
        string code,
        RecordShortLinkVisitDto? visit = null)
    {
        var cancellationToken = CancellationTokenProvider.Token;

        if (code.IsNullOrWhiteSpace() ||
            code.Length is < ShortLinkConsts.MinCodeLength or > ShortLinkConsts.MaxCodeLength)
        {
            return new ShortLinkResolutionDto { Status = ShortLinkResolutionStatus.NotFound };
        }

        var normalizedOrigin = ShortLinkDomainOrigin.Normalize(origin);
        var cacheKey = ShortLinkResolutionCacheKey.Create(normalizedOrigin, code);
        var cacheItem = await _cache.GetAsync(cacheKey, token: cancellationToken);
        if (cacheItem is null)
        {
            cacheItem = await CreateCacheItemAsync(normalizedOrigin, code, cancellationToken);
            await _cache.SetAsync(
                cacheKey,
                cacheItem,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheItem.Exists
                        ? FoundCacheDuration
                        : MissingCacheDuration
                },
                token: cancellationToken);
        }

        if (!cacheItem.Exists)
        {
            return new ShortLinkResolutionDto { Status = ShortLinkResolutionStatus.NotFound };
        }

        var now = Clock.Now.ToUniversalTime();
        if (cacheItem.IsDeleted ||
            cacheItem.Status == ShortLinkStatus.Disabled ||
            (cacheItem.ExpiresAt.HasValue && cacheItem.ExpiresAt.Value <= now))
        {
            return new ShortLinkResolutionDto
            {
                Status = ShortLinkResolutionStatus.Gone,
                ShortLinkId = cacheItem.Id
            };
        }

        using (CurrentTenant.Change(cacheItem.TenantId))
        {
            var targetUri = new Uri(cacheItem.TargetUrl!, UriKind.Absolute);
            var blockedDomain = await _blockedDomainCache.GetAsync(
                targetUri.IdnHost,
                cancellationToken);
            if (blockedDomain.IsBlocked)
            {
                return new ShortLinkResolutionDto
                {
                    Status = ShortLinkResolutionStatus.Blocked,
                    ShortLinkId = cacheItem.Id,
                    BlockedDomain = blockedDomain.MatchedDomain,
                    BlockedReason = blockedDomain.Reason
                };
            }

            if (visit is not null)
            {
                var metadata = _metadataParser.Parse(visit.Referrer, visit.UserAgent);
                var entity = new ShortLinkVisit(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    cacheItem.Id,
                    now,
                    _visitorHashService.Compute(visit.IpAddress, now),
                    metadata.ReferrerHost,
                    metadata.Browser,
                    metadata.DeviceType);

                await _shortLinkRepository.RecordVisitAsync(entity, cancellationToken);
            }
        }

        return new ShortLinkResolutionDto
        {
            Status = ShortLinkResolutionStatus.Found,
            ShortLinkId = cacheItem.Id,
            TargetUrl = cacheItem.TargetUrl
        };
    }

    private async Task<ShortLinkResolutionCacheItem> CreateCacheItemAsync(
        string origin,
        string code,
        CancellationToken cancellationToken)
    {
        var shortLink = await _shortLinkRepository.FindByCodeAsync(
            origin,
            code,
            includeDeleted: true,
            cancellationToken);
        if (shortLink is null && IsConfiguredLegacyOrigin(origin))
        {
            shortLink = await _shortLinkRepository.FindLegacyByCodeAsync(
                code,
                includeDeleted: true,
                cancellationToken);
            if (shortLink is not null)
            {
                _logger.LogWarning(
                    "Resolving legacy short link {ShortLinkId} without an assigned Origin.",
                    shortLink.Id);
            }
        }

        if (shortLink is null)
        {
            return new ShortLinkResolutionCacheItem();
        }

        return new ShortLinkResolutionCacheItem
        {
            Exists = true,
            Id = shortLink.Id,
            TenantId = shortLink.TenantId,
            Origin = shortLink.Origin ?? origin,
            TargetUrl = shortLink.TargetUrl,
            Status = shortLink.Status,
            ExpiresAt = shortLink.ExpiresAt,
            IsDeleted = shortLink.IsDeleted
        };
    }

    private bool IsConfiguredLegacyOrigin(string origin)
    {
        if (_urlOptions.BaseUrl.IsNullOrWhiteSpace())
        {
            _logger.LogError(
                "ShortLink:Urls:BaseUrl is missing; legacy links without an Origin cannot be resolved.");
            return false;
        }

        try
        {
            return ShortLinkDomainOrigin.Normalize(_urlOptions.BaseUrl) == origin;
        }
        catch (BusinessException exception)
        {
            _logger.LogError(
                exception,
                "ShortLink:Urls:BaseUrl is invalid; legacy links without an Origin cannot be resolved.");
            return false;
        }
    }
}
