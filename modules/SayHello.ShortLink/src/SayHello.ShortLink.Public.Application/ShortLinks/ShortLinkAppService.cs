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

    public ShortLinkAppService(
        IShortLinkRepository shortLinkRepository,
        IShortLinkStatisticsRepository statisticsRepository,
        ShortLinkManager shortLinkManager,
        IShortLinkUserEligibilityChecker eligibilityChecker,
        IShortLinkCreationRateLimiter creationRateLimiter,
        IShortLinkUrlBuilder urlBuilder,
        IShortLinkCacheInvalidator cacheInvalidator)
    {
        _shortLinkRepository = shortLinkRepository;
        _statisticsRepository = statisticsRepository;
        _shortLinkManager = shortLinkManager;
        _eligibilityChecker = eligibilityChecker;
        _creationRateLimiter = creationRateLimiter;
        _urlBuilder = urlBuilder;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PagedResultDto<ShortLinkDto>> GetListAsync(GetShortLinksInput input)
    {
        var ownerUserId = CurrentUser.GetId();
        var cancellationToken = CancellationTokenProvider.Token;
        var totalCount = await _shortLinkRepository.GetCountAsync(
            ownerUserId,
            CurrentTenant.Id,
            input.Filter,
            input.Status,
            cancellationToken);
        var entities = await _shortLinkRepository.GetListAsync(
            ownerUserId,
            CurrentTenant.Id,
            input.Filter,
            input.Status,
            input.Sorting,
            input.SkipCount,
            input.MaxResultCount,
            cancellationToken);

        return new PagedResultDto<ShortLinkDto>(
            totalCount,
            entities.Select(x => ShortLinkDtoMapper.ToDto(x, _urlBuilder)).ToList());
    }

    public async Task<ShortLinkDto> GetAsync(Guid id)
    {
        var shortLink = await GetOwnedAsync(id);
        return ShortLinkDtoMapper.ToDto(shortLink, _urlBuilder);
    }

    [Authorize(ShortLinkPublicPermissions.Create)]
    public async Task<ShortLinkDto> CreateAsync(CreateShortLinkDto input)
    {
        var ownerUserId = CurrentUser.GetId();
        await _eligibilityChecker.EnsureEligibleAsync(ownerUserId);
        await _creationRateLimiter.EnsureAllowedAsync(ownerUserId, CurrentTenant.Id);

        var shortLink = await _shortLinkManager.CreateAsync(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            ownerUserId,
            input.TargetUrl,
            input.CustomCode,
            input.Title,
            NormalizeExpiration(input.ExpiresAt),
            CancellationTokenProvider.Token);

        await _shortLinkRepository.InsertAsync(
            shortLink,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await _cacheInvalidator.RemoveAsync(
            shortLink.Code,
            CancellationTokenProvider.Token);
        return ShortLinkDtoMapper.ToDto(shortLink, _urlBuilder);
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
            shortLink.Code,
            CancellationTokenProvider.Token);
        return ShortLinkDtoMapper.ToDto(shortLink, _urlBuilder);
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
            shortLink.Code,
            CancellationTokenProvider.Token);
        return ShortLinkDtoMapper.ToDto(shortLink, _urlBuilder);
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
            shortLink.Code,
            CancellationTokenProvider.Token);
    }

    [Authorize(ShortLinkPublicPermissions.ViewStatistics)]
    public async Task<ShortLinkStatisticsDto> GetStatisticsAsync(Guid id, int days = 30)
    {
        var shortLink = await GetOwnedAsync(id);
        var normalizedDays = Math.Clamp(days, 1, 365);
        var today = DateOnly.FromDateTime(Clock.Now.ToUniversalTime());
        var startDate = today.AddDays(-(normalizedDays - 1));
        var statistics = await _statisticsRepository.GetAsync(
            id,
            startDate,
            today,
            maxDimensionItems: 10,
            CancellationTokenProvider.Token);

        return new ShortLinkStatisticsDto
        {
            ShortLinkId = shortLink.Id,
            Code = shortLink.Code,
            TotalVisitCount = shortLink.TotalVisitCount,
            UniqueVisitorCount = statistics.UniqueVisitorCount,
            Daily = statistics.Daily
                .Select(x => new DailyVisitStatisticDto
                {
                    Date = x.Date,
                    VisitCount = x.VisitCount,
                    UniqueVisitorCount = x.UniqueVisitorCount
                })
                .ToList(),
            Referrers = MapDimensions(statistics.Referrers),
            Browsers = MapDimensions(statistics.Browsers),
            Devices = MapDimensions(statistics.Devices)
        };
    }

    public async Task<ShortLinkQrCodeDto> GetQrCodeAsync(Guid id)
    {
        var shortLink = await GetOwnedAsync(id);
        var shortUrl = _urlBuilder.Build(shortLink.Code);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(shortUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new SvgQRCode(data);

        return new ShortLinkQrCodeDto
        {
            Content = qrCode.GetGraphic(5)
        };
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
