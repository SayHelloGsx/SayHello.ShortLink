using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class ShortLinkNamespaceReservation : BasicAggregateRoot<string>
{
    protected ShortLinkNamespaceReservation()
    {
    }

    public ShortLinkNamespaceReservation(string name) : base(name)
    {
        if (!ShortLinkNamespaceFormat.IsValid(name))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidNamespace);
        }
    }
}
