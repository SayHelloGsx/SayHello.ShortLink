using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.Public.ShortLinkNamespaces;

[Area(ShortLinkPublicRemoteServiceConsts.ModuleName)]
[RemoteService(Name = ShortLinkPublicRemoteServiceConsts.RemoteServiceName)]
[Route("api/short-link/public/namespaces")]
public class ShortLinkNamespaceController(IShortLinkNamespaceAppService appService) :
    ShortLinkPublicController, IShortLinkNamespaceAppService
{
    [HttpGet]
    public Task<ListResultDto<ShortLinkNamespaceDto>> GetListAsync(string? origin = null) =>
        appService.GetListAsync(origin);

    [HttpPost]
    public Task<ShortLinkNamespaceDto> CreateAsync(CreateShortLinkNamespaceDto input) =>
        appService.CreateAsync(input);

    [HttpDelete("{id:guid}")]
    public Task DeleteAsync(Guid id, [FromBody] DeleteShortLinkNamespaceDto input) =>
        appService.DeleteAsync(id, input);
}
