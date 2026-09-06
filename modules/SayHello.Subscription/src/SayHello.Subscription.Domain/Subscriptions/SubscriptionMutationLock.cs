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

    public async Task AcquireAsync(IUnitOfWork unitOfWork, Guid? tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var key = $"Subscription:Mutation:{tenantId?.ToString("N") ?? "host"}:{userId:N}";
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
