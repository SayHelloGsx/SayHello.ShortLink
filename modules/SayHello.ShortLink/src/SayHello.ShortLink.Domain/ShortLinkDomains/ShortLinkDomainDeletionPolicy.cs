using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinkNamespaces;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainDeletionPolicy : DomainService
{
    private readonly IShortLinkDomainRepository _repository;
    private readonly IShortLinkNamespaceRepository _namespaceRepository;

    public ShortLinkDomainDeletionPolicy(
        IShortLinkDomainRepository repository,
        IShortLinkNamespaceRepository namespaceRepository)
    {
        _repository = repository;
        _namespaceRepository = namespaceRepository;
    }

    public async Task EnsureCanDeleteAsync(
        ShortLinkDomain domain,
        CancellationToken cancellationToken = default)
    {
        domain.EnsureCanDelete();
        var referenceCount = await _repository.GetShortLinkReferenceCountAsync(
            domain.Id,
            cancellationToken);
        if (referenceCount > 0 || await _namespaceRepository.HasDomainReferencesAsync(domain.Id, cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainInUse)
                .WithData("Origin", domain.Origin)
                .WithData("ReferenceCount", referenceCount);
        }
    }
}
