using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SayHello.ShortLink.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkQuotaTests : ShortLinkTestBase<ShortLinkQuotaTestModule>
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly QuotaCapabilityProvider _capabilities;
    private readonly QuotaTrackingLock _locks;
    private readonly IUnitOfWorkManager _units;
    private readonly IShortLinkRepository _repository;
    private readonly ShortLinkManager _manager;

    public ShortLinkQuotaTests()
    {
        _capabilities = GetRequiredService<QuotaCapabilityProvider>();
        _locks = GetRequiredService<QuotaTrackingLock>();
        _units = GetRequiredService<IUnitOfWorkManager>();
        _repository = GetRequiredService<IShortLinkRepository>();
        _manager = GetRequiredService<ShortLinkManager>();
    }

    [Theory]
    [InlineData(20)]
    [InlineData(100)]
    public async Task Finite_Quota_Should_Reject_The_Next_Link(int limit)
    {
        _capabilities.Quota = ShortLinkQuota.Limited(limit);
        for (var i = 0; i < limit; i++)
        {
            await CreateAsync();
        }

        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaExceeded);
        (await CountAsync()).ShouldBe(limit);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Denied_And_Zero_Should_Not_Create_And_Unlimited_Should_Not_Use_Old_Limit()
    {
        _capabilities.Quota = ShortLinkQuota.Denied;
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaNotGranted);
        _capabilities.Quota = ShortLinkQuota.Limited(0);
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.LinkQuotaExceeded);
        (await CountAsync()).ShouldBe(0);

        _capabilities.Quota = ShortLinkQuota.Unlimited;
        await CreateAsync();
        (await CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Disabled_Expired_And_Deleted_Links_Should_Use_Confirmed_Counting_Rules()
    {
        _capabilities.Quota = ShortLinkQuota.Limited(2);
        var first = await CreateAsync();
        var second = await CreateAsync(expiresAt: DateTime.UtcNow.AddDays(-1));
        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await _repository.GetAsync(first.Id);
            entity.Disable();
            await _repository.UpdateAsync(entity, autoSave: true);
        });
        (await CountAsync()).ShouldBe(2);
        await Should.ThrowAsync<BusinessException>(() => CreateAsync());

        using (var rollback = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await _repository.DeleteAsync(second.Id, autoSave: true);
            await rollback.RollbackAsync();
        }
        (await CountAsync()).ShouldBe(2);

        await WithUnitOfWorkAsync(() => _repository.DeleteAsync(second.Id, autoSave: true));
        using (GetRequiredService<IDataFilter<ISoftDelete>>().Disable())
        {
            (await CountAsync()).ShouldBe(1);
        }
        await CreateAsync();
        (await CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Existing_Transaction_Should_Hold_One_Lease_Until_Commit_Or_Rollback_Disposal()
    {
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await CreateAsync();
            await CreateAsync();
            _locks.ActiveCount.ShouldBe(1);
            _locks.Acquisitions.ShouldBe(1);
            await unit.CompleteAsync();
            _locks.ActiveCount.ShouldBe(0);
        }

        (await CountAsync()).ShouldBe(2);
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await CreateAsync();
            _locks.ActiveCount.ShouldBe(1);
            await unit.RollbackAsync();
            _locks.ActiveCount.ShouldBe(1);
        }

        _locks.ActiveCount.ShouldBe(0);
        (await CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Failure_Should_Roll_Back_All_Creates_In_Joined_Transaction()
    {
        _capabilities.Quota = ShortLinkQuota.Limited(1);
        using (var unit = _units.Begin(requiresNew: true, isTransactional: true))
        {
            await CreateAsync();
            await Should.ThrowAsync<BusinessException>(() => CreateAsync());
            _locks.ActiveCount.ShouldBe(1);
        }
        (await CountAsync()).ShouldBe(0);
        _locks.ActiveCount.ShouldBe(0);
        await CreateAsync();
    }

    [Fact]
    public async Task Nontransactional_Units_Lock_Failure_And_Validation_Failure_Should_Not_Write()
    {
        using (var unit = _units.Begin(requiresNew: true, isTransactional: false))
        {
            await Should.ThrowAsync<AbpException>(() => CreateAsync());
        }
        _locks.FailAcquisition = true;
        (await Should.ThrowAsync<BusinessException>(() => CreateAsync())).Code
            .ShouldBe(ShortLinkErrorCodes.CreationLockUnavailable);
        _locks.FailAcquisition = false;
        await Should.ThrowAsync<BusinessException>(() => CreateAsync(target: "not a URL"));
        (await CountAsync()).ShouldBe(0);
        _locks.ActiveCount.ShouldBe(0);
        await CreateAsync();
    }

    [Fact]
    public async Task Cancelled_Or_Conflicting_Create_Should_Not_Consume_Another_Slot()
    {
        _capabilities.Quota = ShortLinkQuota.Limited(2);
        var link = await CreateAsync();
        (await Should.ThrowAsync<BusinessException>(() => _manager.CreateAndSaveAsync(
            Guid.NewGuid(), null, _owner, link.TargetUrl, link.Code, null, null))).Code
            .ShouldBe(ShortLinkErrorCodes.CodeAlreadyExists);
        await Should.ThrowAsync<OperationCanceledException>(() => _manager.CreateAndSaveAsync(
            Guid.NewGuid(), null, _owner, link.TargetUrl, null, null, null,
            new CancellationToken(true)));
        _locks.ActiveCount.ShouldBe(0);
        (await CountAsync()).ShouldBe(1);
        await CreateAsync();
        (await CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Downgrade_Should_Block_Creation_But_Not_Changes_Or_Deletion()
    {
        var first = await CreateAsync();
        await CreateAsync();
        _capabilities.Quota = ShortLinkQuota.Limited(1);
        await Should.ThrowAsync<BusinessException>(() => CreateAsync());
        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await _repository.GetAsync(first.Id);
            await _manager.UpdateAsync(entity, "https://example.com/changed", "Changed", null);
            await _repository.UpdateAsync(entity, autoSave: true);
        });
        await WithUnitOfWorkAsync(() => _repository.DeleteAsync(first.Id, autoSave: true));
        await Should.ThrowAsync<BusinessException>(() => CreateAsync());
        _capabilities.Quota = ShortLinkQuota.Limited(2);
        await CreateAsync();
        (await CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Last_Slot_Should_Be_Claimed_Once_Across_Independent_Scopes_And_Transactions()
    {
        _capabilities.Quota = ShortLinkQuota.Limited(1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contenders = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            using var scope = ServiceProvider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<ShortLinkManager>();
            try
            {
                await CreateWithAsync(manager, _owner, null);
                return true;
            }
            catch (BusinessException exception) when (exception.Code == ShortLinkErrorCodes.LinkQuotaExceeded)
            {
                return false;
            }
        })).ToArray();
        start.SetResult();

        (await Task.WhenAll(contenders)).Count(success => success).ShouldBe(1);
        (await CountAsync()).ShouldBe(1);
        _locks.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Quota_Should_Be_Isolated_By_Owner_And_Tenant()
    {
        _capabilities.Quota = ShortLinkQuota.Limited(1);
        await CreateAsync();
        await CreateWithAsync(_manager, Guid.NewGuid(), null);
        var tenantId = Guid.NewGuid();
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            await CreateWithAsync(_manager, _owner, tenantId);
            await Should.ThrowAsync<BusinessException>(() => CreateWithAsync(_manager, _owner, tenantId));
        }
        (await CountAsync()).ShouldBe(1);
        await Should.ThrowAsync<BusinessException>(() => CreateWithAsync(_manager, _owner, tenantId));
    }

    private Task<ShortLink> CreateAsync(DateTime? expiresAt = null, string target = "https://example.com/path") =>
        _manager.CreateAndSaveAsync(Guid.NewGuid(), null, _owner, target,
            "Q" + Guid.NewGuid().ToString("N"), null, expiresAt);

    private static Task<ShortLink> CreateWithAsync(ShortLinkManager manager, Guid owner, Guid? tenantId) =>
        manager.CreateAndSaveAsync(Guid.NewGuid(), tenantId, owner, "https://example.com/path",
            "Q" + Guid.NewGuid().ToString("N"), null, null);

    private Task<long> CountAsync() =>
        WithUnitOfWorkAsync(() => _repository.GetCountByOwnerAsync(_owner));
}

[DependsOn(typeof(ShortLinkEntityFrameworkCoreTestModule))]
public class ShortLinkQuotaTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<QuotaCapabilityProvider>();
        context.Services.Replace(ServiceDescriptor.Singleton<IShortLinkCapabilityProvider>(
            provider => provider.GetRequiredService<QuotaCapabilityProvider>()));
        context.Services.AddSingleton<QuotaTrackingLock>();
        context.Services.Replace(ServiceDescriptor.Singleton<IAbpDistributedLock>(
            provider => provider.GetRequiredService<QuotaTrackingLock>()));
    }
}

public class QuotaCapabilityProvider : IShortLinkCapabilityProvider
{
    public ShortLinkQuota Quota { get; set; } = ShortLinkQuota.Unlimited;
    public bool IsQuotaExternallyManaged => true;
    public Task<ShortLinkQuota> GetQuotaAsync(Guid? tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Quota);
    public Task<bool> IsStatisticsEnabledAsync(Guid? tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

public class QuotaTrackingLock : IAbpDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private int _activeCount;
    private int _acquisitions;
    public int ActiveCount => _activeCount;
    public int Acquisitions => _acquisitions;
    public bool FailAcquisition { get; set; }

    public async Task<IAbpDistributedLockHandle?> TryAcquireAsync(
        string name, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        if (FailAcquisition)
        {
            return null;
        }
        var semaphore = _locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(timeout, cancellationToken))
        {
            return null;
        }
        Interlocked.Increment(ref _activeCount);
        Interlocked.Increment(ref _acquisitions);
        return new Handle(this, semaphore);
    }

    private sealed class Handle(QuotaTrackingLock owner, SemaphoreSlim semaphore) : IAbpDistributedLockHandle
    {
        public ValueTask DisposeAsync()
        {
            Interlocked.Decrement(ref owner._activeCount);
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
