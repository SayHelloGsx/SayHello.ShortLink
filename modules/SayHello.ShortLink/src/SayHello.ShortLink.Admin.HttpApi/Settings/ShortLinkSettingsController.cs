using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace SayHello.ShortLink.Admin.Settings;

[Area(ShortLinkAdminRemoteServiceConsts.ModuleName)]
[RemoteService(Name = ShortLinkAdminRemoteServiceConsts.RemoteServiceName)]
[Route("api/short-link/admin/settings")]
public class ShortLinkSettingsController :
    ShortLinkAdminController,
    IShortLinkSettingsAppService
{
    private readonly IShortLinkSettingsAppService _appService;

    public ShortLinkSettingsController(IShortLinkSettingsAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public Task<ShortLinkSettingsDto> GetAsync()
    {
        return _appService.GetAsync();
    }

    [HttpPut]
    public Task<ShortLinkSettingsDto> UpdateAsync(ShortLinkSettingsDto input)
    {
        return _appService.UpdateAsync(input);
    }
}
