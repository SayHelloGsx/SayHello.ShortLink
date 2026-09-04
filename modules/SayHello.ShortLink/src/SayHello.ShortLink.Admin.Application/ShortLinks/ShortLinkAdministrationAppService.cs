using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.Admin.ShortLinks;

[Authorize(ShortLinkAdminPermissions.ManageAllLinks)]
public class ShortLinkAdministrationAppService :
    ShortLinkApplicationService,
    IShortLinkAdministrationAppService
{
    private readonly IShortLinkRepository _shortLinkRepository;
    private readonly IShortLinkUrlBuilder _urlBuilder;
    private readonly IShortLinkCacheInvalidator _cacheInvalidator;

    public ShortLinkAdministrationAppService(
        IShortLinkRepository shortLinkRepository,
        IShortLinkUrlBuilder urlBuilder,
        IShortLinkCacheInvalidator cacheInvalidator)
    {
        _shortLinkRepository = shortLinkRepository;
        _urlBuilder = urlBuilder;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PagedResultDto<ShortLinkDto>> GetListAsync(GetShortLinksInput input)
    {
        var cancellationToken = CancellationTokenProvider.Token;
        var totalCount = await _shortLinkRepository.GetCountAsync(
            input.OwnerUserId,
            CurrentTenant.Id,
            input.Filter,
            input.Status,
            cancellationToken);
        var entities = await _shortLinkRepository.GetListAsync(
            input.OwnerUserId,
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

    public async Task<ShortLinkDto> SetStatusAsync(Guid id, SetShortLinkStatusDto input)
    {
        var shortLink = await _shortLinkRepository.GetAsync(
            id,
            cancellationToken: CancellationTokenProvider.Token);
        ShortLinkConcurrencyGuard.EnsureMatches(
            shortLink,
            input.ConcurrencyStamp);

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

    public async Task DeleteAsync(Guid id)
    {
        var shortLink = await _shortLinkRepository.GetAsync(
            id,
            cancellationToken: CancellationTokenProvider.Token);
        await _shortLinkRepository.DeleteAsync(
            shortLink,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await _cacheInvalidator.RemoveAsync(
            shortLink.Code,
            CancellationTokenProvider.Token);
    }
}
