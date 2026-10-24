using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkUnitOfWorkLock(IAbpDistributedLock distributedLock) : ITransientDependency
{
    public static void EnsureTransactional(IUnitOfWork unitOfWork)
    {
        if (!unitOfWork.Options.IsTransactional ||
            unitOfWork.Options.IsolationLevel is IsolationLevel.RepeatableRead or
                IsolationLevel.Snapshot or IsolationLevel.ReadUncommitted)
        {
            throw new AbpException("Short-link mutations require a transactional, read-committed or serializable unit of work.");
        }
    }

    public virtual async Task AcquireAsync(
        IUnitOfWork unitOfWork, string key, string errorCode, CancellationToken cancellationToken,
        ShortLinkLockOrder order = ShortLinkLockOrder.Owner)
    {
        if (!Enum.IsDefined(order))
        {
            throw new ArgumentOutOfRangeException(nameof(order));
        }
        if (unitOfWork.Items.ContainsKey(key))
        {
            return;
        }
        if (unitOfWork.Items.Values.OfType<Lease>().Any(lease => lease.Order > order))
        {
            throw new AbpException(
                "Short-link locks must be acquired in namespace-configuration, domain-configuration, owner, then namespace order.");
        }

        var handle = await distributedLock.TryAcquireAsync(key, TimeSpan.FromSeconds(10), cancellationToken)
            ?? throw new BusinessException(errorCode);
        var lease = new Lease(handle, order);
        unitOfWork.Items[key] = lease;
        unitOfWork.OnCompleted(lease.ReleaseAsync);
        // An outer transaction owns the lease until commit or disposal after rollback.
        unitOfWork.Disposed += (_, _) => AsyncHelper.RunSync(lease.ReleaseAsync);
    }

    private sealed class Lease(IAbpDistributedLockHandle handle, ShortLinkLockOrder order)
    {
        private IAbpDistributedLockHandle? _handle = handle;

        public ShortLinkLockOrder Order { get; } = order;

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
