using System;
using System.Threading.Tasks;
using Volo.Abp.Content;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public interface IBlockedDomainAppService : IApplicationService
{
    Task<ListResultDto<BlockedDomainDto>> GetListAsync();

    Task<BlockedDomainDto> CreateAsync(CreateBlockedDomainDto input);

    Task<BlockedDomainDto> UpdateAsync(Guid id, UpdateBlockedDomainDto input);

    Task DeleteAsync(Guid id);

    Task<BlockedDomainImportResultDto> ImportAsync(IRemoteStreamContent file);
}
