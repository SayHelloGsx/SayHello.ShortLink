using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DistributedLocking;

namespace SayHello.ShortLink.EntityFrameworkCore;

public class InProcessAbpDistributedLock : IAbpDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IAbpDistributedLockHandle?> TryAcquireAsync(
        string name,
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
        var acquired = timeout == default
            ? await semaphore.WaitAsync(0, cancellationToken)
            : await semaphore.WaitAsync(timeout, cancellationToken);

        return acquired ? new Handle(semaphore) : null;
    }

    private sealed class Handle : IAbpDistributedLockHandle
    {
        private SemaphoreSlim? _semaphore;

        public Handle(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
