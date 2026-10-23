using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinkNamespaces;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace SayHello.ShortLink.Settings;

public class ShortLinkNamespaceSettingsManager(
    ShortLinkNamespacePolicy policy,
    ShortLinkNamespaceLock namespaceLock,
    IShortLinkNamespaceRepository namespaceRepository,
    IShortLinkNamespaceReservationRepository reservationRepository) : DomainService
{
    public virtual async Task<string> ReserveAsync(string name, CancellationToken cancellationToken = default)
    {
        policy.Validate(name);
        await namespaceLock.AcquireConfigurationAsync(cancellationToken);
        if (await namespaceRepository.NameExistsAsync(name, cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.NamespaceAlreadyExists)
                .WithData("Namespace", name);
        }

        if (!await reservationRepository.IsReservedAsync(name, cancellationToken))
        {
            await reservationRepository.InsertAsync(
                new ShortLinkNamespaceReservation(name), autoSave: true, cancellationToken);
        }

        return name;
    }
}
