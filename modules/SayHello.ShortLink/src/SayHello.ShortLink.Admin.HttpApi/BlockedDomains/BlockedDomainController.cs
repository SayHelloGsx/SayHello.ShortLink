using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;

namespace SayHello.ShortLink.Admin.BlockedDomains;

[Area(ShortLinkAdminRemoteServiceConsts.ModuleName)]
[RemoteService(Name = ShortLinkAdminRemoteServiceConsts.RemoteServiceName)]
[Route("api/short-link/admin/blocked-domains")]
public class BlockedDomainController :
    ShortLinkAdminController,
    IBlockedDomainAppService
{
    private readonly IBlockedDomainAppService _appService;

    public BlockedDomainController(IBlockedDomainAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public Task<ListResultDto<BlockedDomainDto>> GetListAsync()
    {
        return _appService.GetListAsync();
    }

    [HttpPost]
    public Task<BlockedDomainDto> CreateAsync(CreateBlockedDomainDto input)
    {
        return _appService.CreateAsync(input);
    }

    [HttpPut("{id:guid}")]
    public Task<BlockedDomainDto> UpdateAsync(Guid id, UpdateBlockedDomainDto input)
    {
        return _appService.UpdateAsync(id, input);
    }

    [HttpDelete("{id:guid}")]
    public Task DeleteAsync(Guid id)
    {
        return _appService.DeleteAsync(id);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public Task<BlockedDomainImportResultDto> ImportAsync(IRemoteStreamContent file)
    {
        return _appService.ImportAsync(file);
    }
}
