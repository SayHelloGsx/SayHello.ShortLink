using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using SayHello.ShortLink.Common.ShortLinks;
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

    public ShortLinkRedirectAppService(
        IShortLinkRepository shortLinkRepository,
        IDistributedCache<ShortLinkResolutionCacheItem, string> cache,
        IVisitorHashService visitorHashService,
        IVisitMetadataParser metadataParser)
    {
        _shortLinkRepository = shortLinkRepository;
        _cache = cache;
        _visitorHashService = visitorHashService;
        _metadataParser = metadataParser;
    }

    [UnitOfWork(isTransactional: true)]
    public async Task<ShortLinkResolutionDto> ResolveAsync(
        string code,
        RecordShortLinkVisitDto? visit = null)
    {
        var cancellationToken = CancellationTokenProvider.Token;

        if (code.IsNullOrWhiteSpace() ||
            code.Length is < ShortLinkConsts.MinCodeLength or > ShortLinkConsts.MaxCodeLength)
        {
            return new ShortLinkResolutionDto { Status = ShortLinkResolutionStatus.NotFound };
        }

        var cacheItem = await _cache.GetAsync(code, token: cancellationToken);
        if (cacheItem is null)
        {
            cacheItem = await CreateCacheItemAsync(code, cancellationToken);
            await _cache.SetAsync(
                code,
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

        if (visit is not null)
        {
            var metadata = _metadataParser.Parse(visit.Referrer, visit.UserAgent);
            var entity = new ShortLinkVisit(
                GuidGenerator.Create(),
                cacheItem.TenantId,
                cacheItem.Id,
                now,
                _visitorHashService.Compute(visit.IpAddress, now),
                metadata.ReferrerHost,
                metadata.Browser,
                metadata.DeviceType);

            await _shortLinkRepository.RecordVisitAsync(entity, cancellationToken);
        }

        return new ShortLinkResolutionDto
        {
            Status = ShortLinkResolutionStatus.Found,
            ShortLinkId = cacheItem.Id,
            TargetUrl = cacheItem.TargetUrl
        };
    }

    private async Task<ShortLinkResolutionCacheItem> CreateCacheItemAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var shortLink = await _shortLinkRepository.FindByCodeAsync(
            code,
            includeDeleted: true,
            cancellationToken);
        if (shortLink is null)
        {
            return new ShortLinkResolutionCacheItem();
        }

        return new ShortLinkResolutionCacheItem
        {
            Exists = true,
            Id = shortLink.Id,
            TenantId = shortLink.TenantId,
            TargetUrl = shortLink.TargetUrl,
            Status = shortLink.Status,
            ExpiresAt = shortLink.ExpiresAt,
            IsDeleted = shortLink.IsDeleted
        };
    }
}
