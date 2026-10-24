using System;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class ShortLinkNamespaceLock(
    ShortLinkUnitOfWorkLock unitOfWorkLock,
    IUnitOfWorkManager unitOfWorkManager) : ITransientDependency
{
    public virtual Task AcquireConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWorkLock.AcquireAsync(
            GetUnit(), "ShortLink:NamespaceConfiguration", ShortLinkErrorCodes.NamespaceLockUnavailable, cancellationToken,
            ShortLinkLockOrder.NamespaceConfiguration);
    }

    public virtual Task AcquireAsync(Guid namespaceId, CancellationToken cancellationToken = default)
    {
        return unitOfWorkLock.AcquireAsync(
            GetUnit(), $"ShortLink:Namespace:{namespaceId:N}", ShortLinkErrorCodes.NamespaceLockUnavailable, cancellationToken,
            ShortLinkLockOrder.Namespace);
    }

    private IUnitOfWork GetUnit()
    {
        var unit = unitOfWorkManager.Current
            ?? throw new AbpException("Namespace mutations require a transactional unit of work.");
        ShortLinkUnitOfWorkLock.EnsureTransactional(unit);
        return unit;
    }
}
