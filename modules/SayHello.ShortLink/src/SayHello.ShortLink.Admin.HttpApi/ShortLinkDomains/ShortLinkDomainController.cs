using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.Admin.ShortLinkDomains;

[Area(ShortLinkAdminRemoteServiceConsts.ModuleName)]
[RemoteService(Name = ShortLinkAdminRemoteServiceConsts.RemoteServiceName)]
[Route("api/short-link/admin/domains")]
public class ShortLinkDomainController :
    ShortLinkAdminController,
    IShortLinkDomainAppService
{
    private readonly IShortLinkDomainAppService _appService;

    public ShortLinkDomainController(IShortLinkDomainAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public virtual Task<ListResultDto<ShortLinkDomainDto>> GetListAsync()
    {
        return _appService.GetListAsync();
    }

    [HttpPost]
    public virtual Task<ShortLinkDomainDto> CreateAsync(CreateShortLinkDomainDto input)
    {
        return _appService.CreateAsync(input);
    }

    [HttpPut("{id:guid}/enabled")]
    public virtual Task<ShortLinkDomainDto> SetEnabledAsync(
        Guid id,
        SetShortLinkDomainEnabledDto input)
    {
        return _appService.SetEnabledAsync(id, input);
    }

    [HttpPut("{id:guid}/default")]
    public virtual Task<ShortLinkDomainDto> SetDefaultAsync(
        Guid id,
        ShortLinkDomainVersionDto input)
    {
        return _appService.SetDefaultAsync(id, input);
    }

    [HttpDelete("{id:guid}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appService.DeleteAsync(id);
    }
}
