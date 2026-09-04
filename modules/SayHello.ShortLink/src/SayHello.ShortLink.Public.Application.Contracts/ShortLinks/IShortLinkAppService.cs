using System;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.Public.ShortLinks;

public interface IShortLinkAppService : IApplicationService
{
    Task<PagedResultDto<ShortLinkDto>> GetListAsync(GetShortLinksInput input);

    Task<ShortLinkDto> GetAsync(Guid id);

    Task<ShortLinkDto> CreateAsync(CreateShortLinkDto input);

    Task<ShortLinkDto> UpdateAsync(Guid id, UpdateShortLinkDto input);

    Task<ShortLinkDto> SetStatusAsync(Guid id, SetShortLinkStatusDto input);

    Task DeleteAsync(Guid id);

    Task<ShortLinkStatisticsDto> GetStatisticsAsync(Guid id, int days = 30);

    Task<ShortLinkQrCodeDto> GetQrCodeAsync(Guid id);
}
