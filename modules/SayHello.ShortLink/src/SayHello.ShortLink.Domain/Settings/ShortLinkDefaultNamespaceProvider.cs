using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinkNamespaces;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace SayHello.ShortLink.Settings;

public class ShortLinkDefaultNamespaceProvider(
    ISettingProvider settingProvider,
    ShortLinkNamespacePolicy policy,
    IShortLinkNamespaceReservationRepository reservationRepository) : ITransientDependency
{
    public virtual async Task<string> GetAsync(CancellationToken cancellationToken = default)
    {
        var value = await settingProvider.GetOrNullAsync(ShortLinkSettings.DefaultNamespace);
        if (value is null)
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidSettingValue)
                .WithData("SettingName", ShortLinkSettings.DefaultNamespace);
        }

        policy.Validate(value);
        if (!await reservationRepository.IsReservedAsync(value, cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidSettingValue)
                .WithData("SettingName", ShortLinkSettings.DefaultNamespace);
        }

        return value;
    }
}
