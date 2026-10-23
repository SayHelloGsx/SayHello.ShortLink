using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.Public.ShortLinkNamespaces;

[RemoteService(false)]
public interface IShortLinkNamespaceAppService : IApplicationService
{
    Task<ListResultDto<ShortLinkNamespaceDto>> GetListAsync(string? origin = null);

    Task<ShortLinkNamespaceDto> CreateAsync(CreateShortLinkNamespaceDto input);

    Task DeleteAsync(Guid id, DeleteShortLinkNamespaceDto input);
}
