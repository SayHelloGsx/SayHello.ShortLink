using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace SayHello.Subscription.Subscriptions;

public class SubscriptionMutationLock : ITransientDependency
{
    private readonly IAbpDistributedLock _distributedLock;

    public SubscriptionMutationLock(IAbpDistributedLock distributedLock) => _distributedLock = distributedLock;

    public Task AcquireAsync(IUnitOfWork unitOfWork, Guid? tenantId, Guid userId, CancellationToken cancellationToken) =>
        AcquireKeyAsync(unitOfWork, $"Subscription:Mutation:{tenantId?.ToString("N") ?? "host"}:{userId:N}", cancellationToken);

    // Catalog commands may change several aggregate roots in one transaction.
    // Lock the tenant catalog before database access to keep their lock ordering consistent.
    public Task AcquireCatalogAsync(IUnitOfWork unitOfWork, Guid? tenantId, CancellationToken cancellationToken) =>
        AcquireKeyAsync(unitOfWork, $"Subscription:Catalog:{tenantId?.ToString("N") ?? "host"}", cancellationToken);

    private async Task AcquireKeyAsync(IUnitOfWork unitOfWork, string key, CancellationToken cancellationToken)
    {
        if (unitOfWork.Items.ContainsKey(key))
        {
            return;
        }

        var handle = await _distributedLock.TryAcquireAsync(key, TimeSpan.FromSeconds(10), cancellationToken)
            ?? throw new BusinessException(SubscriptionErrorCodes.MutationLockUnavailable);
        var lease = new Lease(handle);
        unitOfWork.Items[key] = lease;
        unitOfWork.OnCompleted(lease.ReleaseAsync);
        // Disposal follows rollback on failure; releasing on the Failed event could be too early.
        unitOfWork.Disposed += (_, _) => AsyncHelper.RunSync(lease.ReleaseAsync);
    }

    private sealed class Lease
    {
        private IAbpDistributedLockHandle? _handle;

        public Lease(IAbpDistributedLockHandle handle) => _handle = handle;

        public async Task ReleaseAsync()
        {
            var handle = Interlocked.Exchange(ref _handle, null);
            if (handle != null)
            {
                await handle.DisposeAsync();
            }
        }
    }
}
