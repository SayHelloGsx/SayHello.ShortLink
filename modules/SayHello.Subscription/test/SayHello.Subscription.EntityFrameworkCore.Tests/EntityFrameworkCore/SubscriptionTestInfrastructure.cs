using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Timing;

namespace SayHello.Subscription.EntityFrameworkCore;

public sealed record SubscriptionTestDatabase(string ConnectionString);

public class SubscriptionTestClock
{
    public DateTime Now { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public IClock Clock { get; } = Substitute.For<IClock>();

    public SubscriptionTestClock()
    {
        Clock.Now.Returns(_ => Now);
        Clock.Kind.Returns(DateTimeKind.Unspecified);
        Clock.SupportsMultipleTimezone.Returns(false);
        Clock.Normalize(Arg.Any<DateTime>()).Returns(call => call.Arg<DateTime>());
    }
}

public class SubscriptionTestDistributedLock : IAbpDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    public int HeldCount => _locks.Values.Count(x => x.CurrentCount == 0);

    public async Task<IAbpDistributedLockHandle?> TryAcquireAsync(string name, TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
        return await semaphore.WaitAsync(timeout, cancellationToken) ? new Handle(semaphore) : null;
    }

    private sealed class Handle : IAbpDistributedLockHandle
    {
        private SemaphoreSlim? _semaphore;
        public Handle(SemaphoreSlim semaphore) => _semaphore = semaphore;
        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
