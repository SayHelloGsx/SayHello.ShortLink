using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;

namespace SayHello.ShortLink.Public.Web.Pages.Public.ShortLinks;

[Authorize(ShortLinkPublicPermissions.ViewStatistics)]
public class StatisticsModel : ShortLinkPublicPageModel
{
    private readonly IShortLinkAppService _appService;

    public ShortLinkStatisticsDto Statistics { get; private set; } = new();

    public ShortLinkDto Link { get; private set; } = new();

    public string? AccessError { get; private set; }

    public StatisticsModel(IShortLinkAppService appService)
    {
        _appService = appService;
    }

    public virtual async Task OnGetAsync(Guid id, int days = 30)
    {
        try
        {
            Statistics = await _appService.GetStatisticsAsync(id, days);
            Link = await _appService.GetAsync(id);
        }
        catch (BusinessException exception) when (exception.Code == ShortLinkErrorCodes.StatisticsNotGranted)
        {
            var converter = LazyServiceProvider.LazyGetRequiredService<IExceptionToErrorInfoConverter>();
            AccessError = converter.Convert(exception).Message;
            Response.StatusCode = StatusCodes.Status403Forbidden;
        }
    }
}
