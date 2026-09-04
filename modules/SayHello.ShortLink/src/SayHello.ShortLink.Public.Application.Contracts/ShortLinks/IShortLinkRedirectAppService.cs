using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.Public.ShortLinks;

[RemoteService(false)]
public interface IShortLinkRedirectAppService : IApplicationService
{
    Task<ShortLinkResolutionDto> ResolveAsync(
        string code,
        RecordShortLinkVisitDto? visit = null);
}
