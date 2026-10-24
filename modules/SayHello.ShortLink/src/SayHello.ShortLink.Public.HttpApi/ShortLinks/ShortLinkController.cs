using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace SayHello.ShortLink.Public.ShortLinks;

[Area(ShortLinkPublicRemoteServiceConsts.ModuleName)]
[RemoteService(Name = ShortLinkPublicRemoteServiceConsts.RemoteServiceName)]
[Route("api/short-link/public/links")]
public class ShortLinkController : ShortLinkPublicController, IShortLinkAppService
{
    private readonly IShortLinkAppService _appService;

    public ShortLinkController(IShortLinkAppService appService)
    {
        _appService = appService;
    }

    [HttpGet("capabilities")]
    public virtual Task<ShortLinkCapabilitiesDto> GetCapabilitiesAsync()
    {
        return _appService.GetCapabilitiesAsync();
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ShortLinkDto>> GetListAsync(GetShortLinksInput input)
    {
        return _appService.GetListAsync(input);
    }

    [HttpGet("{id:guid}")]
    public virtual Task<ShortLinkDto> GetAsync(Guid id)
    {
        return _appService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<ShortLinkDto> CreateAsync(CreateShortLinkDto input)
    {
        return _appService.CreateAsync(input);
    }

    [HttpPut("{id:guid}")]
    public virtual Task<ShortLinkDto> UpdateAsync(Guid id, UpdateShortLinkDto input)
    {
        return _appService.UpdateAsync(id, input);
    }

    [HttpPut("{id:guid}/status")]
    public virtual Task<ShortLinkDto> SetStatusAsync(Guid id, SetShortLinkStatusDto input)
    {
        return _appService.SetStatusAsync(id, input);
    }

    [HttpDelete("{id:guid}")]
    public virtual Task DeleteAsync(Guid id, [FromBody] DeleteShortLinkDto input)
    {
        return _appService.DeleteAsync(id, input);
    }

    [HttpGet("{id:guid}/statistics")]
    public virtual Task<ShortLinkStatisticsDto> GetStatisticsAsync(Guid id, int days = 30)
    {
        return _appService.GetStatisticsAsync(id, days);
    }

    [HttpGet("{id:guid}/qr-code")]
    public virtual Task<ShortLinkQrCodeDto> GetQrCodeAsync(Guid id)
    {
        return _appService.GetQrCodeAsync(id);
    }
}
