using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainConfigurationLock : ITransientDependency
{
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ShortLinkDomainConfigurationLock(
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task AcquireAsync(
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
        if (unitOfWork.Items.ContainsKey(key))
        {
            return;
        }

        var handle = await _distributedLock.TryAcquireAsync(
            key,
            TimeSpan.FromSeconds(10),
            cancellationToken)
            ?? throw new BusinessException(ShortLinkErrorCodes.DomainConfigurationLockUnavailable);
        var lease = new Lease(handle);
        unitOfWork.Items[key] = lease;
        unitOfWork.OnCompleted(lease.ReleaseAsync);
        // Disposal follows rollback, so the lease also covers failed transactions.
        unitOfWork.Disposed += (_, _) => AsyncHelper.RunSync(lease.ReleaseAsync);
    }

    private sealed class Lease(IAbpDistributedLockHandle handle)
    {
        private IAbpDistributedLockHandle? _handle = handle;

        public async Task ReleaseAsync()
        {
            var current = Interlocked.Exchange(ref _handle, null);
            if (current is not null)
            {
                await current.DisposeAsync();
            }
        }
    }
}
