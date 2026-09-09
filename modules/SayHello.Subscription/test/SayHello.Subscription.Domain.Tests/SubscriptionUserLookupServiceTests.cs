using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace SayHello.Subscription.Users;

public class SubscriptionUserLookupServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IExternalUserLookupServiceProvider _externalUsers =
        Substitute.For<IExternalUserLookupServiceProvider>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly SubscriptionUserLookupService _lookup;

    public SubscriptionUserLookupServiceTests()
    {
        _currentTenant.Id.Returns(_tenantId);
        _lookup = new SubscriptionUserLookupService(_externalUsers, _currentTenant);
    }

    [Fact]
    public async Task Find_by_id_maps_the_complete_snapshot_and_forwards_cancellation()
    {
        var userId = Guid.NewGuid();
        var properties = new ExtraPropertyDictionary
        {
            ["department"] = "operations"
        };
        var external = new UserData(
            userId,
            "subscriber",
            "subscriber@example.test",
            "Test",
            "Subscriber",
            emailConfirmed: true,
            phoneNumber: "+10000000000",
            phoneNumberConfirmed: true,
            tenantId: _tenantId,
            isActive: true,
            extraProperties: properties);
        using var cancellation = new CancellationTokenSource();
        _externalUsers.FindByIdAsync(userId, cancellation.Token).Returns(external);

        var user = await _lookup.FindByIdAsync(userId, cancellation.Token);

        user.Id.ShouldBe(userId);
        user.TenantId.ShouldBe(_tenantId);
        user.UserName.ShouldBe(external.UserName);
        user.Name.ShouldBe(external.Name);
        user.Surname.ShouldBe(external.Surname);
        user.Email.ShouldBe(external.Email);
        user.IsActive.ShouldBeTrue();
        user.EmailConfirmed.ShouldBeTrue();
        user.PhoneNumber.ShouldBe(external.PhoneNumber);
        user.PhoneNumberConfirmed.ShouldBeTrue();
        user.ExtraProperties.ShouldNotBeSameAs(properties);
        user.ExtraProperties["department"].ShouldBe("operations");
        await _externalUsers.Received(1).FindByIdAsync(userId, cancellation.Token);
    }

    [Fact]
    public async Task Missing_users_remain_missing()
    {
        var userId = Guid.NewGuid();
        _externalUsers.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((IUserData)null!);

        (await _lookup.FindByIdAsync(userId)).ShouldBeNull();
    }

    [Fact]
    public async Task Find_by_user_name_delegates_normalization_to_the_provider()
    {
        var external = User(Guid.NewGuid(), _tenantId, "NormalizedName");
        using var cancellation = new CancellationTokenSource();
        _externalUsers.FindByUserNameAsync(" normalizedname ", cancellation.Token)
            .Returns(external);

        var user = await _lookup.FindByUserNameAsync(" normalizedname ", cancellation.Token);

        user.UserName.ShouldBe("NormalizedName");
        await _externalUsers.Received(1)
            .FindByUserNameAsync(" normalizedname ", cancellation.Token);
    }

    [Fact]
    public async Task Search_and_count_forward_inputs_and_keep_inactive_users_visible()
    {
        var inactive = User(Guid.NewGuid(), _tenantId, isActive: false);
        using var cancellation = new CancellationTokenSource();
        _externalUsers.GetCountAsync("inactive", cancellation.Token).Returns(3L);
        _externalUsers.SearchAsync(
                "userName asc, id asc",
                "inactive",
                2,
                1,
                cancellation.Token)
            .Returns(new List<IUserData> { inactive });

        var count = await _lookup.GetCountAsync("inactive", cancellation.Token);
        var users = await _lookup.SearchAsync(
            "userName asc, id asc",
            "inactive",
            2,
            1,
            cancellation.Token);

        count.ShouldBe(3);
        users.ShouldHaveSingleItem().IsActive.ShouldBeFalse();
        await _externalUsers.Received(1).GetCountAsync("inactive", cancellation.Token);
        await _externalUsers.Received(1).SearchAsync(
            "userName asc, id asc",
            "inactive",
            2,
            1,
            cancellation.Token);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("tenant")]
    public async Task Inconsistent_provider_results_fail_closed(string mismatch)
    {
        var requestedId = Guid.NewGuid();
        var external = mismatch == "id"
            ? User(Guid.NewGuid(), _tenantId)
            : User(requestedId, Guid.NewGuid());
        _externalUsers.FindByIdAsync(requestedId, Arg.Any<CancellationToken>())
            .Returns(external);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _lookup.FindByIdAsync(requestedId));

        exception.Code.ShouldBe(mismatch == "id"
            ? SubscriptionErrorCodes.UserNotFound
            : SubscriptionErrorCodes.TenantMismatch);
    }

    [Fact]
    public async Task One_cross_tenant_search_result_rejects_the_whole_page()
    {
        _externalUsers.SearchAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<IUserData>
            {
                User(Guid.NewGuid(), _tenantId),
                User(Guid.NewGuid(), Guid.NewGuid())
            });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _lookup.SearchAsync(maxResultCount: 10));

        exception.Code.ShouldBe(SubscriptionErrorCodes.TenantMismatch);
    }

    [Theory]
    [InlineData("null-page")]
    [InlineData("null-user")]
    [InlineData("negative-count")]
    public async Task Malformed_provider_results_are_explicit_errors(string result)
    {
        if (result == "negative-count")
        {
            _externalUsers.GetCountAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(-1L);

            await Should.ThrowAsync<AbpException>(() => _lookup.GetCountAsync());
            return;
        }

        _externalUsers.SearchAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(result == "null-page"
                ? null!
                : new List<IUserData> { null! });

        await Should.ThrowAsync<AbpException>(() => _lookup.SearchAsync(maxResultCount: 10));
    }

    [Fact]
    public async Task Empty_ids_are_rejected_before_the_provider()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _lookup.FindByIdAsync(Guid.Empty));

        exception.Code.ShouldBe(SubscriptionErrorCodes.InvalidAssignment);
        _externalUsers.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("lookup")]
    [InlineData("cancellation")]
    public async Task Provider_failures_propagate_without_a_missing_user_fallback(string failure)
    {
        Exception error = failure == "lookup"
            ? new InvalidOperationException("Identity unavailable.")
            : new OperationCanceledException();
        var userId = Guid.NewGuid();
        _externalUsers.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IUserData>(error));

        (await Record.ExceptionAsync(() => _lookup.FindByIdAsync(userId)))
            .ShouldBeSameAs(error);
    }

    private static UserData User(
        Guid id,
        Guid? tenantId,
        string userName = "subscriber",
        bool isActive = true) =>
        new(
            id,
            userName,
            "subscriber@example.test",
            "Test",
            "Subscriber",
            tenantId: tenantId,
            isActive: isActive);
}
