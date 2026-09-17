using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinks;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Public.Entitlements;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Users;
using Xunit;

namespace SayHello.ShortLink.Subscription;

public class SubscriptionShortLinkCapabilityProviderTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ICurrentUserEntitlementAppService _client =
        Substitute.For<ICurrentUserEntitlementAppService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly SubscriptionShortLinkCapabilityProvider _provider;

    public SubscriptionShortLinkCapabilityProviderTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Id.Returns(_userId);
        _provider = new SubscriptionShortLinkCapabilityProvider(_client, _currentUser);
    }

    public class ShortLinkSubscriptionEntitlementOptionProviderTests
    {
        private readonly IShortLinkDomainRepository _domains =
            Substitute.For<IShortLinkDomainRepository>();
        private readonly ShortLinkSubscriptionEntitlementOptionProvider _provider;

        public ShortLinkSubscriptionEntitlementOptionProviderTests()
        {
            _provider = new ShortLinkSubscriptionEntitlementOptionProvider(_domains);
        }

        [Fact]
        public async Task Statistics_options_should_use_stable_values_in_display_order()
        {
            var options = await _provider.GetOptionsAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics);

            options.ShouldBe(
            [
                ShortLinkSubscriptionDefinitions.StatisticsNone,
                ShortLinkSubscriptionDefinitions.StatisticsBasic,
                ShortLinkSubscriptionDefinitions.StatisticsAdvanced
            ]);
        }

        [Fact]
        public async Task Domain_options_should_include_only_enabled_current_tenant_origins()
        {
            var first = new ShortLinkDomain(
                Guid.NewGuid(),
                null,
                "https://z.example.test");
            var second = new ShortLinkDomain(
                Guid.NewGuid(),
                null,
                "https://a.example.test");
            var disabled = new ShortLinkDomain(
                Guid.NewGuid(),
                null,
                "https://disabled.example.test");
            disabled.Disable();
            _domains.GetListAsync(Arg.Any<CancellationToken>())
                .Returns(new List<ShortLinkDomain> { first, disabled, second });

            var options = await _provider.GetOptionsAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Domains);

            options.ShouldBe(["https://a.example.test", "https://z.example.test"]);
        }

        [Fact]
        public async Task Unknown_products_and_features_should_have_no_options()
        {
            _provider.CanProvide(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics).ShouldBeTrue();
            _provider.CanProvide(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Domains).ShouldBeTrue();
            _provider.CanProvide(
                "other-product",
                ShortLinkSubscriptionDefinitions.Statistics).ShouldBeFalse();
            _provider.CanProvide(
                ShortLinkSubscriptionDefinitions.ProductCode,
                "other-feature").ShouldBeFalse();

            (await _provider.GetOptionsAsync(
                "other-product",
                ShortLinkSubscriptionDefinitions.Statistics)).ShouldBeEmpty();
            (await _provider.GetOptionsAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                "other-feature")).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Missing_or_ungranted_quota_is_denied()
    {
        Numeric(new NumericEntitlementResultDto());

        var quota = await _provider.GetQuotaAsync(_userId);

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

        var quota = await _provider.GetQuotaAsync(_userId);

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

        var quota = await _provider.GetQuotaAsync(_userId);

        quota.IsGranted.ShouldBeTrue();
        quota.IsUnlimited.ShouldBeTrue();
        quota.Limit.ShouldBeNull();
    }

    [Theory]
    [InlineData(ShortLinkSubscriptionDefinitions.StatisticsNone, ShortLinkStatisticsLevel.None)]
    [InlineData(ShortLinkSubscriptionDefinitions.StatisticsBasic, ShortLinkStatisticsLevel.Basic)]
    [InlineData(ShortLinkSubscriptionDefinitions.StatisticsAdvanced, ShortLinkStatisticsLevel.Advanced)]
    public async Task Statistics_follow_the_enum_entitlement(
        string value,
        ShortLinkStatisticsLevel expected)
    {
        _client.GetEnumAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics,
                Arg.Any<CancellationToken>())
            .Returns(new EnumEntitlementResultDto { IsGranted = true, Value = value });

        (await _provider.GetStatisticsLevelAsync(_userId)).ShouldBe(expected);
    }

    [Fact]
    public async Task Missing_statistics_entitlement_is_none()
    {
        _client.GetEnumAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics,
                Arg.Any<CancellationToken>())
            .Returns(new EnumEntitlementResultDto());

        (await _provider.GetStatisticsLevelAsync(_userId))
            .ShouldBe(ShortLinkStatisticsLevel.None);
    }

    [Fact]
    public async Task Unknown_statistics_value_is_rejected()
    {
        _client.GetEnumAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics,
                Arg.Any<CancellationToken>())
            .Returns(new EnumEntitlementResultDto { IsGranted = true, Value = "unknown" });

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _provider.GetStatisticsLevelAsync(_userId));
    }

    [Fact]
    public async Task Domain_entitlement_returns_normalized_restricted_origins()
    {
        _client.GetStringSetAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Domains,
                Arg.Any<CancellationToken>())
            .Returns(new StringSetEntitlementResultDto
            {
                IsGranted = true,
                Values = ["HTTPS://GO.EXAMPLE.COM:443/", "http://localhost:5000"]
            });

        var access = await _provider.GetDomainAccessAsync(_userId);

        access.IsRestricted.ShouldBeTrue();
        access.Origins.ShouldBe(
            ["https://go.example.com", "http://localhost:5000"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Queries_forward_shared_keys_and_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        Numeric(new NumericEntitlementResultDto { IsGranted = true, Limit = 20 });
        _client.GetEnumAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Statistics,
                cancellation.Token)
            .Returns(new EnumEntitlementResultDto
            {
                IsGranted = true,
                Value = ShortLinkSubscriptionDefinitions.StatisticsAdvanced
            });
        _client.GetStringSetAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.Domains,
                cancellation.Token)
            .Returns(new StringSetEntitlementResultDto());

        await _provider.GetQuotaAsync(_userId, cancellation.Token);
        await _provider.GetStatisticsLevelAsync(_userId, cancellation.Token);
        await _provider.GetDomainAccessAsync(_userId, cancellation.Token);

        await _client.Received(1).GetNumericAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.MaxLinks,
            cancellation.Token);
        await _client.Received(1).GetEnumAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.Statistics,
            cancellation.Token);
        await _client.Received(1).GetStringSetAsync(
            ShortLinkSubscriptionDefinitions.ProductCode,
            ShortLinkSubscriptionDefinitions.Domains,
            cancellation.Token);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("user")]
    public async Task Non_current_subjects_are_rejected_before_the_contract_is_called(string mismatch)
    {
        if (mismatch == "anonymous")
        {
            _currentUser.IsAuthenticated.Returns(false);
        }
        var userId = mismatch == "user" ? Guid.NewGuid() : _userId;

        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            _provider.GetQuotaAsync(userId));

        _client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_granted_finite_result_without_a_limit_is_invalid()
    {
        Numeric(new NumericEntitlementResultDto { IsGranted = true });

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _provider.GetQuotaAsync(_userId));
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

        (await Record.ExceptionAsync(() => _provider.GetQuotaAsync(_userId)))
            .ShouldBeSameAs(error);
    }

    private void Numeric(NumericEntitlementResultDto result) =>
        _client.GetNumericAsync(
                ShortLinkSubscriptionDefinitions.ProductCode,
                ShortLinkSubscriptionDefinitions.MaxLinks,
                Arg.Any<CancellationToken>())
            .Returns(result);
}
