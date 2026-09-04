using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

[Authorize(ShortLinkPublicPermissions.ViewStatistics)]
public class StatisticsModel : ShortLinkPublicPageModel
{
    private readonly IShortLinkAppService _appService;

    public ShortLinkStatisticsDto Statistics { get; private set; } = new();

    public StatisticsModel(IShortLinkAppService appService)
    {
        _appService = appService;
    }

    public async Task OnGetAsync(Guid id, int days = 30)
    {
        Statistics = await _appService.GetStatisticsAsync(id, days);
    }
}
