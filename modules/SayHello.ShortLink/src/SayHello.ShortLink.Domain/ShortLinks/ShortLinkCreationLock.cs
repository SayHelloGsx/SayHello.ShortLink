using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkCreationLock : ITransientDependency
{
    private readonly IAbpDistributedLock _distributedLock;

    public ShortLinkCreationLock(IAbpDistributedLock distributedLock)
    {
        _distributedLock = distributedLock;
    }

    public async Task AcquireAsync(
        IUnitOfWork unitOfWork,
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var key = $"ShortLink:Creation:{tenantId?.ToString("N") ?? "host"}:{userId:N}";
        if (unitOfWork.Items.ContainsKey(key))
        {
            return;
        }

        var handle = await _distributedLock.TryAcquireAsync(key, TimeSpan.FromSeconds(10), cancellationToken)
            ?? throw new BusinessException(ShortLinkErrorCodes.CreationLockUnavailable);
        var lease = new Lease(handle);
        unitOfWork.Items[key] = lease;
        unitOfWork.OnCompleted(lease.ReleaseAsync);
        // An outer UOW may commit after the service returns; disposal also follows rollback.
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
