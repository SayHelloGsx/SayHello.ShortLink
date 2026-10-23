using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class EfCoreShortLinkNamespaceReservationRepository :
    EfCoreRepository<IShortLinkDbContext, ShortLinkNamespaceReservation, string>,
    IShortLinkNamespaceReservationRepository
{
    public EfCoreShortLinkNamespaceReservationRepository(
        IDbContextProvider<IShortLinkDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<bool> IsReservedAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync()).AnyAsync(
            x => x.Id == name,
            GetCancellationToken(cancellationToken));
    }
}
