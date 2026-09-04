using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.Admin.ShortLinks;

[Area(ShortLinkAdminRemoteServiceConsts.ModuleName)]
[RemoteService(Name = ShortLinkAdminRemoteServiceConsts.RemoteServiceName)]
[Route("api/short-link/admin/links")]
public class ShortLinkAdministrationController :
    ShortLinkAdminController,
    IShortLinkAdministrationAppService
{
    private readonly IShortLinkAdministrationAppService _appService;

    public ShortLinkAdministrationController(IShortLinkAdministrationAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public Task<PagedResultDto<ShortLinkDto>> GetListAsync(GetShortLinksInput input)
    {
        return _appService.GetListAsync(input);
    }

    [HttpPut("{id:guid}/status")]
    public Task<ShortLinkDto> SetStatusAsync(Guid id, SetShortLinkStatusDto input)
    {
        return _appService.SetStatusAsync(id, input);
    }

    [HttpDelete("{id:guid}")]
    public Task DeleteAsync(Guid id)
    {
        return _appService.DeleteAsync(id);
    }
}
