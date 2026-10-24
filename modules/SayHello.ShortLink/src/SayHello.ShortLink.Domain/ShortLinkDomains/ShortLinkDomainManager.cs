using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainManager : DomainService
{
    private readonly IShortLinkDomainRepository _repository;
    private readonly ShortLinkDomainDeletionPolicy _deletionPolicy;

    public ShortLinkDomainManager(
        IShortLinkDomainRepository repository,
        ShortLinkDomainDeletionPolicy deletionPolicy)
    {
        _repository = repository;
        _deletionPolicy = deletionPolicy;
    }

    public virtual async Task<ShortLinkDomain> CreateAsync(
        string origin,
        CancellationToken cancellationToken = default)
    {
        var normalizedOrigin = ShortLinkDomainOrigin.Normalize(origin);
        if (await _repository.OriginExistsAsync(
                normalizedOrigin,
                cancellationToken: cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainAlreadyExists)
                .WithData("Origin", normalizedOrigin);
        }

        var count = await _repository.GetCountAsync(cancellationToken);
        if (count > 0 &&
            await _repository.FindDefaultAsync(cancellationToken) is null)
        {
            throw new BusinessException(ShortLinkErrorCodes.DefaultDomainRequired);
        }

        return new ShortLinkDomain(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            normalizedOrigin,
            isDefault: count == 0);
    }

    public virtual Task<ShortLinkDomain?> FindDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.FindDefaultAsync(cancellationToken);
    }

    public virtual void ClearDefault(ShortLinkDomain domain)
    {
        domain.ClearDefault();
    }

    public virtual void SetDefault(ShortLinkDomain domain)
    {
        domain.SetAsDefault();
    }

    public virtual Task EnsureCanDeleteAsync(
        ShortLinkDomain domain,
        CancellationToken cancellationToken = default)
    {
        return _deletionPolicy.EnsureCanDeleteAsync(domain, cancellationToken);
    }
}
