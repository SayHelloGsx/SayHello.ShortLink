using System;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.Admin.ShortLinks;

public interface IShortLinkAdministrationAppService : IApplicationService
{
    Task<PagedResultDto<ShortLinkDto>> GetListAsync(GetShortLinksInput input);

    Task<ShortLinkDto> SetStatusAsync(Guid id, SetShortLinkStatusDto input);

    Task DeleteAsync(Guid id);
}
