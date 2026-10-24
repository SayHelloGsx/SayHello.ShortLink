using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainAccessPolicy(
    IShortLinkDomainRepository repository,
    IShortLinkCapabilityProvider capabilityProvider) : DomainService
{
    public virtual async Task<ShortLinkDomain> GetAuthorizedAsync(
        Guid userId, string origin, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainAccessDenied);
        }

        var normalizedOrigin = ShortLinkDomainOrigin.Normalize(origin);
        var domain = await repository.FindByOriginAsync(normalizedOrigin, enabledOnly: true, cancellationToken);
        if (domain is null ||
            (!domain.IsDefault && !(await capabilityProvider.GetDomainAccessAsync(userId, cancellationToken)).Allows(domain.Origin)))
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainAccessDenied)
                .WithData("Origin", normalizedOrigin);
        }

        return domain;
    }

    public virtual async Task<IReadOnlyList<ShortLinkDomain>> GetAvailableAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainAccessDenied);
        }

        var domains = (await repository.GetListAsync(cancellationToken)).Where(x => x.IsEnabled).ToList();
        if (domains.Count(x => x.IsDefault) != 1)
        {
            throw new BusinessException(ShortLinkErrorCodes.DefaultDomainRequired);
        }

        var access = await capabilityProvider.GetDomainAccessAsync(userId, cancellationToken);
        return domains.Where(domain => domain.IsDefault || access.Allows(domain.Origin)).ToList();
    }
}
