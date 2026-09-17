using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainDeletionPolicy : DomainService
{
    private readonly IShortLinkDomainRepository _repository;

    public ShortLinkDomainDeletionPolicy(IShortLinkDomainRepository repository)
    {
        _repository = repository;
    }

    public async Task EnsureCanDeleteAsync(
        ShortLinkDomain domain,
        CancellationToken cancellationToken = default)
    {
        domain.EnsureCanDelete();
        var referenceCount = await _repository.GetShortLinkReferenceCountAsync(
            domain.Id,
            cancellationToken);
        if (referenceCount > 0)
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainInUse)
                .WithData("Origin", domain.Origin)
                .WithData("ReferenceCount", referenceCount);
        }
    }
}
