using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using QRCoder;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Users;
using ShortLinkEntity = SayHello.ShortLink.ShortLinks.ShortLink;

namespace SayHello.ShortLink.Public.ShortLinks;

[Authorize(ShortLinkPublicPermissions.Default)]
public class ShortLinkAppService : ShortLinkApplicationService, IShortLinkAppService
{
    private readonly IShortLinkRepository _shortLinkRepository;
    private readonly IShortLinkStatisticsRepository _statisticsRepository;
    private readonly ShortLinkManager _shortLinkManager;
    private readonly IShortLinkUserEligibilityChecker _eligibilityChecker;
    private readonly IShortLinkCreationRateLimiter _creationRateLimiter;
    private readonly IShortLinkUrlBuilder _urlBuilder;
    private readonly IShortLinkCacheInvalidator _cacheInvalidator;
    private readonly IShortLinkCapabilityProvider _capabilityProvider;
    private readonly IPermissionChecker _permissionChecker;

    public ShortLinkAppService(
        IShortLinkRepository shortLinkRepository,
        IShortLinkStatisticsRepository statisticsRepository,
        ShortLinkManager shortLinkManager,
        IShortLinkUserEligibilityChecker eligibilityChecker,
        IShortLinkCreationRateLimiter creationRateLimiter,
        IShortLinkUrlBuilder urlBuilder,
        IShortLinkCacheInvalidator cacheInvalidator,
        IShortLinkCapabilityProvider capabilityProvider,
        IPermissionChecker permissionChecker)
    {
        _shortLinkRepository = shortLinkRepository;
        _statisticsRepository = statisticsRepository;
        _shortLinkManager = shortLinkManager;
        _eligibilityChecker = eligibilityChecker;
        _creationRateLimiter = creationRateLimiter;
        _urlBuilder = urlBuilder;
        _cacheInvalidator = cacheInvalidator;
        _capabilityProvider = capabilityProvider;
        _permissionChecker = permissionChecker;
    }

    public async Task<ShortLinkCapabilitiesDto> GetCapabilitiesAsync()
    {
        var ownerUserId = CurrentUser.GetId();
        var cancellationToken = CancellationTokenProvider.Token;
        var quota = await _capabilityProvider.GetQuotaAsync(ownerUserId, cancellationToken);
        var usedLinks = await _shortLinkRepository.GetCountByOwnerAsync(
            ownerUserId, cancellationToken);

        var statisticsLevel = await _capabilityProvider.GetStatisticsLevelAsync(
            ownerUserId, cancellationToken);
        var domains = await _shortLinkManager.GetAvailableDomainsAsync(
            ownerUserId, cancellationToken);

        return new ShortLinkCapabilitiesDto
        {
            UsedLinks = usedLinks,
            IsQuotaGranted = quota.IsGranted,
            IsUnlimited = quota.IsUnlimited,
            MaxLinks = quota.Limit,
            RemainingLinks = !quota.IsGranted
                ? 0
                : quota.IsUnlimited ? null : Math.Max(0, quota.Limit!.Value - usedLinks),
            StatisticsLevel = statisticsLevel,
            Domains = domains.Select(domain => new ShortLinkDomainOptionDto
            {
                Origin = domain.Origin,
                IsDefault = domain.IsDefault
            }).ToList()
        };
    }

    public async Task<PagedResultDto<ShortLinkDto>> GetListAsync(GetShortLinksInput input)
    {
        var ownerUserId = CurrentUser.GetId();
        var cancellationToken = CancellationTokenProvider.Token;
        var statisticsLevel = await GetSummaryStatisticsLevelAsync();
        if (statisticsLevel == ShortLinkStatisticsLevel.None && IsStatisticsSorting(input.Sorting))
        {
            throw new BusinessException(ShortLinkErrorCodes.StatisticsNotGranted);
        }

        var totalCount = await _shortLinkRepository.GetCountAsync(
            ownerUserId,
            input.Filter,
            input.Status,
            cancellationToken);
        var entities = await _shortLinkRepository.GetListAsync(
            ownerUserId,
            input.Filter,
            input.Status,
            input.Sorting,
            input.SkipCount,
            input.MaxResultCount,
            cancellationToken);

        return new PagedResultDto<ShortLinkDto>(
            totalCount,
            entities.Select(x => ShortLinkDtoMapper.ToPublicDto(x, _urlBuilder, statisticsLevel)).ToList());
    }

    public async Task<ShortLinkDto> GetAsync(Guid id)
    {
        var shortLink = await GetOwnedAsync(id);
        return await ToPublicDtoAsync(shortLink);
    }

    [Authorize(ShortLinkPublicPermissions.Create)]
    public async Task<ShortLinkDto> CreateAsync(CreateShortLinkDto input)
    {
        var ownerUserId = CurrentUser.GetId();
        await _eligibilityChecker.EnsureEligibleAsync(ownerUserId);
        await _creationRateLimiter.EnsureAllowedAsync(ownerUserId);

        var shortLink = await _shortLinkManager.CreateAndSaveAsync(
            GuidGenerator.Create(),
            ownerUserId,
            input.Origin,
            input.TargetUrl,
            input.CustomCode,
            input.Title,
            NormalizeExpiration(input.ExpiresAt),
            CancellationTokenProvider.Token);

        await _cacheInvalidator.RemoveAsync(
            shortLink.Origin!,
            shortLink.Code,
            CancellationTokenProvider.Token);
        return await ToPublicDtoAsync(shortLink);
    }

    [Authorize(ShortLinkPublicPermissions.Update)]
    public async Task<ShortLinkDto> UpdateAsync(Guid id, UpdateShortLinkDto input)
    {
        var shortLink = await GetOwnedAsync(id);
        ShortLinkConcurrencyGuard.EnsureMatches(shortLink, input.ConcurrencyStamp);
        await _eligibilityChecker.EnsureEligibleAsync(CurrentUser.GetId());
        await _shortLinkManager.UpdateAsync(
            shortLink,
            input.TargetUrl,
            input.Title,
            NormalizeExpiration(input.ExpiresAt),
            CancellationTokenProvider.Token);

        await _shortLinkRepository.UpdateAsync(
            shortLink,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await _cacheInvalidator.RemoveAsync(
            shortLink.Origin!,
            shortLink.Code,
            CancellationTokenProvider.Token);
        return await ToPublicDtoAsync(shortLink);
    }

    [Authorize(ShortLinkPublicPermissions.Update)]
    public async Task<ShortLinkDto> SetStatusAsync(Guid id, SetShortLinkStatusDto input)
    {
        var shortLink = await GetOwnedAsync(id);
        ShortLinkConcurrencyGuard.EnsureMatches(shortLink, input.ConcurrencyStamp);
        await _eligibilityChecker.EnsureEligibleAsync(CurrentUser.GetId());

        if (input.Status == ShortLinkStatus.Active)
        {
            shortLink.Activate();
        }
        else
        {
            shortLink.Disable();
        }

        await _shortLinkRepository.UpdateAsync(
            shortLink,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await _cacheInvalidator.RemoveAsync(
            shortLink.Origin!,
            shortLink.Code,
            CancellationTokenProvider.Token);
        return await ToPublicDtoAsync(shortLink);
    }

    [Authorize(ShortLinkPublicPermissions.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var shortLink = await GetOwnedAsync(id);
        await _eligibilityChecker.EnsureEligibleAsync(CurrentUser.GetId());
        await _shortLinkRepository.DeleteAsync(
            shortLink,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await _cacheInvalidator.RemoveAsync(
            shortLink.Origin!,
            shortLink.Code,
            CancellationTokenProvider.Token);
    }

    [Authorize(ShortLinkPublicPermissions.ViewStatistics)]
    public async Task<ShortLinkStatisticsDto> GetStatisticsAsync(Guid id, int days = 30)
    {
        var shortLink = await GetOwnedAsync(id);
        var statisticsLevel = await GetStatisticsLevelAsync();
        if (statisticsLevel == ShortLinkStatisticsLevel.None)
        {
            throw new BusinessException(ShortLinkErrorCodes.StatisticsNotGranted);
        }

        var result = new ShortLinkStatisticsDto
        {
            ShortLinkId = shortLink.Id,
            Code = shortLink.Code,
            StatisticsLevel = statisticsLevel,
            TotalVisitCount = shortLink.TotalVisitCount
        };
        if (statisticsLevel == ShortLinkStatisticsLevel.Basic)
        {
            return result;
        }

        var normalizedDays = Math.Clamp(days, 1, 365);
        var today = DateOnly.FromDateTime(Clock.Now.ToUniversalTime());
        var startDate = today.AddDays(-(normalizedDays - 1));
        var statistics = await _statisticsRepository.GetAsync(
            id,
            startDate,
            today,
            maxDimensionItems: 10,
            CancellationTokenProvider.Token);

        result.UniqueVisitorCount = statistics.UniqueVisitorCount;
        result.Daily = statistics.Daily
            .Select(x => new DailyVisitStatisticDto
            {
                Date = x.Date,
                VisitCount = x.VisitCount,
                UniqueVisitorCount = x.UniqueVisitorCount
            })
            .ToList();
        result.Referrers = MapDimensions(statistics.Referrers);
        result.Browsers = MapDimensions(statistics.Browsers);
        result.Devices = MapDimensions(statistics.Devices);
        return result;
    }

    public async Task<ShortLinkQrCodeDto> GetQrCodeAsync(Guid id)
    {
        var shortLink = await GetOwnedAsync(id);
        var shortUrl = shortLink.Origin.IsNullOrWhiteSpace()
            ? _urlBuilder.Build(shortLink.Code)
            : _urlBuilder.Build(shortLink.Origin, shortLink.Code);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(shortUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new SvgQRCode(data);

        return new ShortLinkQrCodeDto
        {
            Content = qrCode.GetGraphic(5)
        };
    }

    private Task<ShortLinkStatisticsLevel> GetStatisticsLevelAsync()
    {
        return _capabilityProvider.GetStatisticsLevelAsync(
            CurrentUser.GetId(), CancellationTokenProvider.Token);
    }

    private async Task<ShortLinkStatisticsLevel> GetSummaryStatisticsLevelAsync()
    {
        var statisticsLevel = await GetStatisticsLevelAsync();
        if (statisticsLevel == ShortLinkStatisticsLevel.None)
        {
            return ShortLinkStatisticsLevel.None;
        }

        return await _permissionChecker.IsGrantedAsync(
            ShortLinkPublicPermissions.ViewStatistics)
                ? statisticsLevel
                : ShortLinkStatisticsLevel.None;
    }

    private async Task<ShortLinkDto> ToPublicDtoAsync(ShortLinkEntity shortLink)
    {
        return ShortLinkDtoMapper.ToPublicDto(
            shortLink, _urlBuilder, await GetSummaryStatisticsLevelAsync());
    }

    private static bool IsStatisticsSorting(string? sorting)
    {
        var parts = sorting?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: > 0 } &&
            string.Equals(parts[0], "totalvisitcount", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ShortLinkEntity> GetOwnedAsync(Guid id)
    {
        var shortLink = await _shortLinkRepository.GetAsync(
            id,
            cancellationToken: CancellationTokenProvider.Token);
        if (shortLink.OwnerUserId != CurrentUser.GetId())
        {
            throw new BusinessException(ShortLinkErrorCodes.LinkAccessDenied);
        }

        return shortLink;
    }

    private static List<DimensionStatisticDto> MapDimensions(
        IReadOnlyList<ShortLinkDimensionVisitData> dimensions)
    {
        return dimensions
            .Select(x => new DimensionStatisticDto
            {
                Value = x.Value,
                VisitCount = x.VisitCount
            })
            .ToList();
    }

    private static DateTime? NormalizeExpiration(DateTime? expiresAt)
    {
        if (!expiresAt.HasValue)
        {
            return null;
        }

        return expiresAt.Value.Kind switch
        {
            DateTimeKind.Utc => expiresAt.Value,
            DateTimeKind.Local => expiresAt.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)
        };
    }
}
