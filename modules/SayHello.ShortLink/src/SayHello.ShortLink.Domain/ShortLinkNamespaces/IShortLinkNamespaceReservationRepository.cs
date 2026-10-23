using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public interface IShortLinkNamespaceReservationRepository : IRepository<ShortLinkNamespaceReservation, string>
{
    Task<bool> IsReservedAsync(string name, CancellationToken cancellationToken = default);
}
