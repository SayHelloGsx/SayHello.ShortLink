using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.ShortLink.WebHost.Subscriptions;
using SayHello.Subscription;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace SayHello.ShortLink.WebHost.Subscriptions;

public class IdentitySubscriptionUserDirectoryTests
{
    private readonly IIdentityUserRepository _users = Substitute.For<IIdentityUserRepository>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IdentitySubscriptionUserDirectory _directory;

    public IdentitySubscriptionUserDirectoryTests()
    {
        _directory = new IdentitySubscriptionUserDirectory(_users, _currentTenant);
    }

    [Fact]
    public async Task Lookup_Should_Map_Identity_User_And_Forward_Cancellation()
    {
        var id = Guid.NewGuid();
        var user = new IdentityUser(id, "subscriber", "subscriber@example.test")
        {
            Name = "Test",
            Surname = "Subscriber"
        };
        using var cancellation = new CancellationTokenSource();
        _users.FindAsync(id, false, cancellation.Token).Returns(user);

        var result = await _directory.FindAsync(null, id, cancellation.Token);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(id);
        result.TenantId.ShouldBeNull();
        result.UserName.ShouldBe("subscriber");
        result.Email.ShouldBe("subscriber@example.test");
        result.Name.ShouldBe("Test");
        result.Surname.ShouldBe("Subscriber");
        result.IsActive.ShouldBe(user.IsActive);
        await _users.Received(1).FindAsync(id, false, cancellation.Token);
    }

    [Fact]
    public async Task Missing_User_Should_Not_Be_Treated_As_Valid()
    {
        var id = Guid.NewGuid();
        _users.FindAsync(id, false).Returns((IdentityUser?)null);

        (await _directory.FindAsync(null, id)).ShouldBeNull();
    }

    [Fact]
    public async Task Every_Operation_Should_Reject_Another_Tenant()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        (await Should.ThrowAsync<BusinessException>(() => _directory.FindAsync(tenantId, userId)))
            .Code.ShouldBe(SubscriptionErrorCodes.TenantMismatch);
        (await Should.ThrowAsync<BusinessException>(() => _directory.SearchAsync(tenantId, null, 0, 10)))
            .Code.ShouldBe(SubscriptionErrorCodes.TenantMismatch);
        (await Should.ThrowAsync<BusinessException>(() => _directory.GetByIdsAsync(tenantId, [userId])))
            .Code.ShouldBe(SubscriptionErrorCodes.TenantMismatch);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Lookup_Should_Reject_Inconsistent_Repository_Tenant()
    {
        var id = Guid.NewGuid();
        _users.FindAsync(id, false).Returns(
            new IdentityUser(id, "other", "other@example.test", Guid.NewGuid()));

        var exception = await Should.ThrowAsync<BusinessException>(() => _directory.FindAsync(null, id));

        exception.Code.ShouldBe(SubscriptionErrorCodes.TenantMismatch);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task Search_Should_Reject_Unbounded_Or_Invalid_Paging(int skipCount, int maxResultCount)
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => _directory.SearchAsync(null, null, skipCount, maxResultCount));

        exception.Code.ShouldBe(SubscriptionErrorCodes.InvalidPaging);
        _users.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_Should_Return_Stable_Paged_Results()
    {
        var user = new IdentityUser(Guid.NewGuid(), "subscriber", "subscriber@example.test");
        _users.GetCountAsync(filter: "subscriber").Returns(3L);
        _users.GetListAsync(
                sorting: "userName asc, id asc", maxResultCount: 2, skipCount: 1,
                filter: "subscriber", includeDetails: false)
            .Returns(new List<IdentityUser> { user });

        var page = await _directory.SearchAsync(null, " subscriber ", 1, 2);

        page.TotalCount.ShouldBe(3);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Batch_Lookup_Should_Validate_Results_And_Use_One_Repository_Query()
    {
        var id = Guid.NewGuid();
        var user = new IdentityUser(id, "subscriber", "subscriber@example.test");
        _users.GetListByIdsAsync(Arg.Any<IEnumerable<Guid>>(), false)
            .Returns(new List<IdentityUser> { user });

        var result = await _directory.GetByIdsAsync(null, [id]);

        result.ShouldHaveSingleItem().Id.ShouldBe(id);
        await _users.Received(1).GetListByIdsAsync(Arg.Any<IEnumerable<Guid>>(), false);
        (await _directory.GetByIdsAsync(null, [])).ShouldBeEmpty();
    }
}
