using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkCreationLock : ITransientDependency
{
    private readonly ShortLinkUnitOfWorkLock _unitOfWorkLock;
    private readonly ICurrentTenant _currentTenant;

    public ShortLinkCreationLock(
        ShortLinkUnitOfWorkLock unitOfWorkLock,
        ICurrentTenant currentTenant)
    {
        _unitOfWorkLock = unitOfWorkLock;
        _currentTenant = currentTenant;
    }

    public async Task AcquireAsync(
        IUnitOfWork unitOfWork,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.Id;
        var key = $"ShortLink:Creation:{tenantId?.ToString("N") ?? "host"}:{userId:N}";
        await _unitOfWorkLock.AcquireAsync(
            unitOfWork, key, ShortLinkErrorCodes.CreationLockUnavailable, cancellationToken,
            ShortLinkLockOrder.Owner);
    }
}
