using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Public.Entitlements;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class SubscriptionShortLinkCapabilityProviderTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid? _tenantId = Guid.NewGuid();
    private readonly ICurrentUserEntitlementAppService _client =
        Substitute.For<ICurrentUserEntitlementAppService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly SubscriptionShortLinkCapabilityProvider _provider;

    public SubscriptionShortLinkCapabilityProviderTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Id.Returns(_userId);
        _currentTenant.Id.Returns(_tenantId);
        _provider = new SubscriptionShortLinkCapabilityProvider(_client, _currentUser, _currentTenant);
    }

    [Fact]
    public async Task Missing_or_ungranted_quota_is_denied()
    {
        Numeric(new NumericEntitlementResultDto());

        var quota = await _provider.GetQuotaAsync(_tenantId, _userId);

        _provider.IsQuotaExternallyManaged.ShouldBeTrue();
        quota.IsGranted.ShouldBeFalse();
        quota.IsUnlimited.ShouldBeFalse();
        quota.Limit.ShouldBeNull();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(20L)]
    [InlineData(100L)]
    [InlineData(long.MaxValue)]
    public async Task Finite_quotas_preserve_the_contract_limit(long limit)
    {
        Numeric(new NumericEntitlementResultDto
        {
            IsGranted = true,
            Limit = limit
        });

        var quota = await _provider.GetQuotaAsync(_tenantId, _userId);

        quota.IsGranted.ShouldBeTrue();
        quota.IsUnlimited.ShouldBeFalse();
        quota.Limit.ShouldBe(limit);
    }

    [Fact]
    public async Task Only_an_explicit_unlimited_grant_is_unlimited()
    {
        Numeric(new NumericEntitlementResultDto
        {
            IsGranted = true,
            IsUnlimited = true
        });

        var quota = await _provider.GetQuotaAsync(_tenantId, _userId);

        quota.IsGranted.ShouldBeTrue();
        quota.IsUnlimited.ShouldBeTrue();
        quota.Limit.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Statistics_follow_the_public_contract_grant(bool granted)
    {
        _client.GetBooleanAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics,
                Arg.Any<CancellationToken>())
            .Returns(new BooleanEntitlementResultDto { IsGranted = granted });

        (await _provider.IsStatisticsEnabledAsync(_tenantId, _userId)).ShouldBe(granted);
    }

    [Fact]
    public async Task Queries_forward_shared_keys_and_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        Numeric(new NumericEntitlementResultDto { IsGranted = true, Limit = 20 });
        _client.GetBooleanAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics,
                cancellation.Token)
            .Returns(new BooleanEntitlementResultDto { IsGranted = true });

        await _provider.GetQuotaAsync(_tenantId, _userId, cancellation.Token);
        await _provider.IsStatisticsEnabledAsync(_tenantId, _userId, cancellation.Token);

        await _client.Received(1).GetNumericAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.MaxLinks,
            cancellation.Token);
        await _client.Received(1).GetBooleanAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.Statistics,
            cancellation.Token);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("user")]
    [InlineData("tenant")]
    public async Task Non_current_subjects_are_rejected_before_the_contract_is_called(string mismatch)
    {
        if (mismatch == "anonymous")
        {
            _currentUser.IsAuthenticated.Returns(false);
        }

        var userId = mismatch == "user" ? Guid.NewGuid() : _userId;
        var tenantId = mismatch == "tenant" ? Guid.NewGuid() : _tenantId;

        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            _provider.GetQuotaAsync(tenantId, userId));

        _client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_granted_finite_result_without_a_limit_is_invalid()
    {
        Numeric(new NumericEntitlementResultDto { IsGranted = true });

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _provider.GetQuotaAsync(_tenantId, _userId));
    }

    [Theory]
    [InlineData("remote")]
    [InlineData("cancellation")]
    public async Task Contract_errors_propagate_without_fallback(string failure)
    {
        Exception error = failure == "remote"
            ? new InvalidOperationException("Subscription service unavailable.")
            : new OperationCanceledException();
        _client.GetNumericAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.MaxLinks,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<NumericEntitlementResultDto>(error));

        (await Record.ExceptionAsync(() => _provider.GetQuotaAsync(_tenantId, _userId)))
            .ShouldBeSameAs(error);
    }

    private void Numeric(NumericEntitlementResultDto result) =>
        _client.GetNumericAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.MaxLinks,
                Arg.Any<CancellationToken>())
            .Returns(result);
}
