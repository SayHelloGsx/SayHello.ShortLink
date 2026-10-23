using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkUnitOfWorkLockTests
{
    private readonly RecordingDistributedLock _distributedLock = new();
    private readonly ShortLinkUnitOfWorkLock _subject;

    public ShortLinkUnitOfWorkLockTests()
    {
        _subject = new ShortLinkUnitOfWorkLock(_distributedLock);
    }

    public static IEnumerable<object[]> RankTransitions()
    {
        foreach (var first in Enum.GetValues<ShortLinkLockOrder>())
        {
            foreach (var next in Enum.GetValues<ShortLinkLockOrder>())
            {
                yield return [first, next];
            }
        }
    }

    [Theory]
    [MemberData(nameof(RankTransitions))]
    public async Task New_Keys_Should_Only_Acquire_Equal_Or_Higher_Ranks(
        ShortLinkLockOrder first, ShortLinkLockOrder next)
    {
        using (var context = new UnitContext())
        {
            context.Unit.Items["Unrelated:State"] = new object();
            await AcquireAsync(context, "Test:First", first);
            if ((int)next < (int)first)
            {
                (await Should.ThrowAsync<AbpException>(() => AcquireAsync(context, "Test:Next", next)))
                    .ShouldBeOfType<AbpException>();
                _distributedLock.Attempts.Count.ShouldBe(1);
                context.Completions.Count.ShouldBe(1);
                context.Unit.Items.ContainsKey("Test:Next").ShouldBeFalse();
            }
            else
            {
                await AcquireAsync(context, "Test:Next", next);
                _distributedLock.Attempts.Count.ShouldBe(2);
                context.Completions.Count.ShouldBe(2);
            }
            _distributedLock.Handles.All(handle => handle.DisposeCalls == 0).ShouldBeTrue();
        }
        _distributedLock.Handles.All(handle => handle.DisposeCalls == 1).ShouldBeTrue();
    }

    [Fact]
    public async Task Rank_Inversions_Should_Fail_Before_Entering_The_Distributed_Wait()
    {
        using var context = new UnitContext();
        await AcquireAsync(context, "Test:Owner", ShortLinkLockOrder.Owner);
        _distributedLock.ThrowOnAcquisition = true;

        var rejected = AcquireAsync(context, "Test:Configuration", ShortLinkLockOrder.NamespaceConfiguration);
        rejected.IsCompleted.ShouldBeTrue();
        (await Should.ThrowAsync<AbpException>(() => rejected)).ShouldBeOfType<AbpException>();
        _distributedLock.Attempts.ShouldHaveSingleItem().ShouldBe("Test:Owner");
        context.Unit.Items.ContainsKey("Test:Configuration").ShouldBeFalse();
        _distributedLock.Handles.ShouldHaveSingleItem().DisposeCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Already_Held_Lower_Rank_Keys_Should_Be_Reused_Without_Waiting()
    {
        using var context = new UnitContext();
        await AcquireAsync(context, "Test:Configuration", ShortLinkLockOrder.NamespaceConfiguration);
        await AcquireAsync(context, "Test:Namespace", ShortLinkLockOrder.Namespace);
        _distributedLock.ThrowOnAcquisition = true;

        await AcquireAsync(context, "Test:Configuration", ShortLinkLockOrder.NamespaceConfiguration);
        await AcquireAsync(context, "Test:Namespace", ShortLinkLockOrder.Namespace);

        _distributedLock.Attempts.Count.ShouldBe(2);
        context.Completions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Reacquiring_A_Key_Should_Not_Promote_Its_Original_Lease_Rank()
    {
        using var context = new UnitContext();
        await AcquireAsync(context, "Test:Configuration", ShortLinkLockOrder.NamespaceConfiguration);
        await AcquireAsync(context, "Test:Configuration", ShortLinkLockOrder.Namespace);
        await AcquireAsync(context, "Test:Domain", ShortLinkLockOrder.DomainConfiguration);

        _distributedLock.Attempts.ShouldBe(new[] { "Test:Configuration", "Test:Domain" });
        context.Completions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task The_Existing_Four_Argument_Call_Should_Retain_The_Owner_Rank()
    {
        using var context = new UnitContext();
        await _subject.AcquireAsync(
            context.Unit, "Test:Owner", ShortLinkErrorCodes.CreationLockUnavailable, CancellationToken.None);
        (await Should.ThrowAsync<AbpException>(() =>
            AcquireAsync(context, "Test:Domain", ShortLinkLockOrder.DomainConfiguration)))
            .ShouldBeOfType<AbpException>();
        await AcquireAsync(context, "Test:SecondOwner", ShortLinkLockOrder.Owner);

        _distributedLock.Attempts.ShouldBe(new[] { "Test:Owner", "Test:SecondOwner" });
    }

    [Theory]
    [InlineData(ShortLinkLockOrder.NamespaceConfiguration)]
    [InlineData(ShortLinkLockOrder.DomainConfiguration)]
    [InlineData(ShortLinkLockOrder.Owner)]
    [InlineData(ShortLinkLockOrder.Namespace)]
    public async Task Every_Wrapper_Should_Declare_Its_Exact_Rank(ShortLinkLockOrder order)
    {
        using var context = new UnitContext();
        var units = Substitute.For<IUnitOfWorkManager>();
        units.Current.Returns(context.Unit);
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        var namespaceLock = new ShortLinkNamespaceLock(_subject, units);

        switch (order)
        {
            case ShortLinkLockOrder.NamespaceConfiguration:
                await namespaceLock.AcquireConfigurationAsync();
                break;
            case ShortLinkLockOrder.DomainConfiguration:
                await new ShortLinkDomainConfigurationLock(_subject, tenant, units)
                    .AcquireAsync(CancellationToken.None);
                break;
            case ShortLinkLockOrder.Owner:
                await new ShortLinkCreationLock(_subject, tenant)
                    .AcquireAsync(context.Unit, Guid.NewGuid(), CancellationToken.None);
                break;
            case ShortLinkLockOrder.Namespace:
                await namespaceLock.AcquireAsync(Guid.NewGuid());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }

        if (order != ShortLinkLockOrder.NamespaceConfiguration)
        {
            (await Should.ThrowAsync<AbpException>(() =>
                AcquireAsync(context, "Test:LowerRank", (ShortLinkLockOrder)((int)order - 1))))
                .ShouldBeOfType<AbpException>();
        }
        await AcquireAsync(context, "Test:SameRank", order);
        _distributedLock.Attempts.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Wrappers_Should_Compose_And_Reuse_Configuration_Domain_Owner_Namespace_In_Order()
    {
        using var context = new UnitContext();
        var units = Substitute.For<IUnitOfWorkManager>();
        units.Current.Returns(context.Unit);
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        var namespaceLock = new ShortLinkNamespaceLock(_subject, units);
        var domainLock = new ShortLinkDomainConfigurationLock(_subject, tenant, units);
        var creationLock = new ShortLinkCreationLock(_subject, tenant);
        var owner = Guid.NewGuid();
        var namespaceId = Guid.NewGuid();

        await namespaceLock.AcquireConfigurationAsync();
        await domainLock.AcquireAsync(CancellationToken.None);
        await creationLock.AcquireAsync(context.Unit, owner, CancellationToken.None);
        await namespaceLock.AcquireAsync(namespaceId);
        await namespaceLock.AcquireConfigurationAsync();
        await domainLock.AcquireAsync(CancellationToken.None);
        await creationLock.AcquireAsync(context.Unit, owner, CancellationToken.None);
        await namespaceLock.AcquireAsync(namespaceId);

        _distributedLock.Attempts.ShouldBe(new[]
        {
            "ShortLink:NamespaceConfiguration",
            "ShortLink:Domains:host",
            $"ShortLink:Creation:host:{owner:N}",
            $"ShortLink:Namespace:{namespaceId:N}"
        });
        context.Completions.Count.ShouldBe(4);
        (await Should.ThrowAsync<AbpException>(() =>
            creationLock.AcquireAsync(context.Unit, Guid.NewGuid(), CancellationToken.None)))
            .ShouldBeOfType<AbpException>();
        _distributedLock.Attempts.Count.ShouldBe(4);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task Invalid_Ranks_Should_Be_Rejected_Even_For_Already_Held_Keys(int rank)
    {
        using var context = new UnitContext();
        await AcquireAsync(context, "Test:Owner", ShortLinkLockOrder.Owner);
        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            AcquireAsync(context, "Test:Owner", (ShortLinkLockOrder)rank));

        exception.ParamName.ShouldBe("order");
        _distributedLock.Attempts.Count.ShouldBe(1);
        context.Completions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Different_Lock_Instances_Should_Share_The_Units_Rank_And_Lease_State()
    {
        using var context = new UnitContext();
        await AcquireAsync(context, "Test:Namespace", ShortLinkLockOrder.Namespace);
        var other = new ShortLinkUnitOfWorkLock(_distributedLock);

        (await Should.ThrowAsync<AbpException>(() => other.AcquireAsync(
            context.Unit, "Test:Owner", ShortLinkErrorCodes.CreationLockUnavailable,
            CancellationToken.None, ShortLinkLockOrder.Owner))).ShouldBeOfType<AbpException>();
        await other.AcquireAsync(
            context.Unit, "Test:Namespace", ShortLinkErrorCodes.CreationLockUnavailable,
            CancellationToken.None, ShortLinkLockOrder.Namespace);
        _distributedLock.Attempts.Count.ShouldBe(1);
        context.Completions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_Failed_Acquisition_Should_Not_Retain_A_Rank_Or_A_Completion_Callback()
    {
        using var context = new UnitContext();
        await AcquireAsync(context, "Test:Configuration", ShortLinkLockOrder.NamespaceConfiguration);
        _distributedLock.FailAcquisition = true;
        (await Should.ThrowAsync<BusinessException>(() =>
            AcquireAsync(context, "Test:Owner", ShortLinkLockOrder.Owner))).Code
            .ShouldBe(ShortLinkErrorCodes.CreationLockUnavailable);
        context.Unit.Items.ContainsKey("Test:Owner").ShouldBeFalse();
        context.Completions.Count.ShouldBe(1);
        _distributedLock.FailAcquisition = false;

        await AcquireAsync(context, "Test:Domain", ShortLinkLockOrder.DomainConfiguration);
        _distributedLock.Attempts.Count.ShouldBe(3);
        _distributedLock.Handles.Count.ShouldBe(2);
        context.Completions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Acquisition_Should_Forward_Cancellation_And_Not_Retain_A_Cancelled_Rank()
    {
        using var context = new UnitContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => _subject.AcquireAsync(
            context.Unit, "Test:Cancelled", ShortLinkErrorCodes.CreationLockUnavailable,
            cancellation.Token, ShortLinkLockOrder.Namespace));
        _distributedLock.LastToken.ShouldBe(cancellation.Token);
        _distributedLock.LastTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        context.Unit.Items.ShouldBeEmpty();
        context.Completions.ShouldBeEmpty();
        await AcquireAsync(context, "Test:Configuration", ShortLinkLockOrder.NamespaceConfiguration);
    }

    [Fact]
    public async Task Completion_Then_Disposal_Should_Release_Every_Lease_Exactly_Once()
    {
        using var context = new UnitContext();
        foreach (var order in Enum.GetValues<ShortLinkLockOrder>())
        {
            await AcquireAsync(context, $"Test:{order}", order);
        }
        _distributedLock.Handles.All(handle => handle.DisposeCalls == 0).ShouldBeTrue();

        await context.CompleteAsync();
        _distributedLock.Handles.All(handle => handle.DisposeCalls == 1).ShouldBeTrue();
        context.Dispose();
        _distributedLock.Handles.All(handle => handle.DisposeCalls == 1).ShouldBeTrue();
    }

    [Fact]
    public async Task Rolled_Back_Units_Should_Retain_Leases_Until_Disposal()
    {
        using (var context = new UnitContext())
        {
            await AcquireAsync(context, "Test:Owner", ShortLinkLockOrder.Owner);
            await context.Unit.RollbackAsync();
            _distributedLock.Handles.ShouldHaveSingleItem().DisposeCalls.ShouldBe(0);
        }
        _distributedLock.Handles.ShouldHaveSingleItem().DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task A_New_Unit_Should_Not_Inherit_A_Previous_Units_Maximum_Rank()
    {
        using (var first = new UnitContext())
        {
            await AcquireAsync(first, "Test:Namespace", ShortLinkLockOrder.Namespace);
            await first.CompleteAsync();
        }
        using (var second = new UnitContext())
        {
            await AcquireAsync(second, "Test:Configuration", ShortLinkLockOrder.NamespaceConfiguration);
        }
        _distributedLock.Attempts.Count.ShouldBe(2);
        _distributedLock.Handles.All(handle => handle.DisposeCalls == 1).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, IsolationLevel.ReadUncommitted)]
    [InlineData(true, IsolationLevel.RepeatableRead)]
    [InlineData(true, IsolationLevel.Snapshot)]
    public void Unsupported_Transactions_Should_Still_Be_Rejected(bool transactional, IsolationLevel? isolation)
    {
        using var context = new UnitContext(transactional, isolation);
        Should.Throw<AbpException>(() => ShortLinkUnitOfWorkLock.EnsureTransactional(context.Unit));
        _distributedLock.Attempts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.Serializable)]
    public void Supported_Transactions_Should_Still_Be_Accepted(IsolationLevel? isolation)
    {
        using var context = new UnitContext(true, isolation);
        Should.NotThrow(() => ShortLinkUnitOfWorkLock.EnsureTransactional(context.Unit));
    }

    private Task AcquireAsync(UnitContext context, string key, ShortLinkLockOrder order) =>
        _subject.AcquireAsync(
            context.Unit, key, ShortLinkErrorCodes.CreationLockUnavailable, CancellationToken.None, order);

    private sealed class UnitContext : IDisposable
    {
        public IUnitOfWork Unit { get; } = Substitute.For<IUnitOfWork>();
        public List<Func<Task>> Completions { get; } = [];

        public UnitContext(bool transactional = true, IsolationLevel? isolation = null)
        {
            Unit.Items.Returns(new Dictionary<string, object>());
            Unit.Options.Returns(new AbpUnitOfWorkOptions
            {
                IsTransactional = transactional,
                IsolationLevel = isolation
            });
            Unit.When(unit => unit.OnCompleted(Arg.Any<Func<Task>>()))
                .Do(call => Completions.Add(call.Arg<Func<Task>>()));
            Unit.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }

        public async Task CompleteAsync()
        {
            foreach (var callback in Completions)
            {
                await callback();
            }
        }

        public void Dispose() => Unit.Disposed += Raise.EventWith(Unit, new UnitOfWorkEventArgs(Unit));
    }

    private sealed class RecordingDistributedLock : IAbpDistributedLock
    {
        public List<string> Attempts { get; } = [];
        public List<RecordingHandle> Handles { get; } = [];
        public bool ThrowOnAcquisition { get; set; }
        public bool FailAcquisition { get; set; }
        public CancellationToken LastToken { get; private set; }
        public TimeSpan LastTimeout { get; private set; }

        public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
            string name, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            Attempts.Add(name);
            LastToken = cancellationToken;
            LastTimeout = timeout;
            if (ThrowOnAcquisition)
            {
                throw new InvalidOperationException("The rejected rank entered a distributed wait.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (FailAcquisition)
            {
                return Task.FromResult<IAbpDistributedLockHandle?>(null);
            }
            var handle = new RecordingHandle();
            Handles.Add(handle);
            return Task.FromResult<IAbpDistributedLockHandle?>(handle);
        }
    }

    private sealed class RecordingHandle : IAbpDistributedLockHandle
    {
        public int DisposeCalls { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
