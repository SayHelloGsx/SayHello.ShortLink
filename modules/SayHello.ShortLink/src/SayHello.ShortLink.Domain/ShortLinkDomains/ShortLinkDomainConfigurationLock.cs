using System;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainConfigurationLock : ITransientDependency
{
    private readonly ShortLinkUnitOfWorkLock _unitOfWorkLock;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ShortLinkDomainConfigurationLock(
        ShortLinkUnitOfWorkLock unitOfWorkLock,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _unitOfWorkLock = unitOfWorkLock;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public virtual async Task AcquireAsync(
        CancellationToken cancellationToken)
    {
        var unitOfWork = _unitOfWorkManager.Current
            ?? throw new AbpException(
                "Short-link domain mutations require a transactional unit of work.");
        if (!unitOfWork.Options.IsTransactional)
        {
            throw new AbpException(
                "Short-link domain mutations require a transactional unit of work.");
        }

        var tenantId = _currentTenant.Id;
        var key = $"ShortLink:Domains:{tenantId?.ToString("N") ?? "host"}";
        await _unitOfWorkLock.AcquireAsync(
            unitOfWork, key, ShortLinkErrorCodes.DomainConfigurationLockUnavailable, cancellationToken,
            ShortLinkLockOrder.DomainConfiguration);
    }
}
