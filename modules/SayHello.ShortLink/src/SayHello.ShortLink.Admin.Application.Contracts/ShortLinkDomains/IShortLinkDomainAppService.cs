using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.Admin.ShortLinkDomains;

public interface IShortLinkDomainAppService : IApplicationService
{
    Task<ListResultDto<ShortLinkDomainDto>> GetListAsync();

    Task<ShortLinkDomainDto> CreateAsync(CreateShortLinkDomainDto input);

    Task<ShortLinkDomainDto> SetEnabledAsync(
        Guid id,
        SetShortLinkDomainEnabledDto input);

    Task<ShortLinkDomainDto> SetDefaultAsync(Guid id);

    Task DeleteAsync(Guid id);
}
