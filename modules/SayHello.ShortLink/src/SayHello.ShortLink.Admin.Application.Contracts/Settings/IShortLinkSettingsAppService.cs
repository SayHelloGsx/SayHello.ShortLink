using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.Admin.Settings;

public interface IShortLinkSettingsAppService : IApplicationService
{
    Task<ShortLinkSettingsDto> GetAsync();

    Task<ShortLinkSettingsDto> UpdateAsync(ShortLinkSettingsDto input);
}
