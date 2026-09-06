using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.Subscription.Users;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;
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

public class SubscriptionTestUserDirectory : ISubscriptionUserDirectory
{
    private readonly ConcurrentDictionary<(Guid?, Guid), SubscriptionUserData> _users = new();
    private readonly ICurrentTenant _tenant;

    public SubscriptionTestUserDirectory(ICurrentTenant tenant) => _tenant = tenant;

    public void Add(Guid? tenantId, Guid userId) =>
        _users[(tenantId, userId)] = new SubscriptionUserData(userId, tenantId, "test-user", "Test", "User", null, true);

    public Task<SubscriptionUserData?> FindAsync(Guid? tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_tenant.Id, tenantId);
        return Task.FromResult(_users.GetValueOrDefault((tenantId, userId)));
    }

    public Task<SubscriptionPage<SubscriptionUserData>> SearchAsync(Guid? tenantId, string? filter,
        int skipCount, int maxResultCount, CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_tenant.Id, tenantId);
        SubscriptionGuard.Paging(skipCount, maxResultCount);
        var users = _users.Values.Where(x => x.TenantId == tenantId &&
            (string.IsNullOrEmpty(filter) || x.UserName.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToArray();
        return Task.FromResult(new SubscriptionPage<SubscriptionUserData>(users.Length, users.Skip(skipCount).Take(maxResultCount)));
    }

    public Task<IReadOnlyList<SubscriptionUserData>> GetByIdsAsync(Guid? tenantId, IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_tenant.Id, tenantId);
        return Task.FromResult<IReadOnlyList<SubscriptionUserData>>(_users.Values
            .Where(x => x.TenantId == tenantId && userIds.Contains(x.Id)).ToArray());
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
