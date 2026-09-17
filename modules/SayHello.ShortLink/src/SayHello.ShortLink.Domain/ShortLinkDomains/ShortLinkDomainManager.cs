using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainManager : DomainService
{
    private readonly IShortLinkDomainRepository _repository;
    private readonly ShortLinkDomainDeletionPolicy _deletionPolicy;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public ShortLinkDomainManager(
        IShortLinkDomainRepository repository,
        ShortLinkDomainDeletionPolicy deletionPolicy,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _repository = repository;
        _deletionPolicy = deletionPolicy;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task<ShortLinkDomain> CreateAsync(
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
            _guidGenerator.Create(),
            _currentTenant.Id,
            normalizedOrigin,
            isDefault: count == 0);
    }

    public Task<ShortLinkDomain?> FindDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.FindDefaultAsync(cancellationToken);
    }

    public void ClearDefault(ShortLinkDomain domain)
    {
        domain.ClearDefault();
    }

    public void SetDefault(ShortLinkDomain domain)
    {
        domain.SetAsDefault();
    }

    public Task EnsureCanDeleteAsync(
        ShortLinkDomain domain,
        CancellationToken cancellationToken = default)
    {
        return _deletionPolicy.EnsureCanDeleteAsync(domain, cancellationToken);
    }
}
