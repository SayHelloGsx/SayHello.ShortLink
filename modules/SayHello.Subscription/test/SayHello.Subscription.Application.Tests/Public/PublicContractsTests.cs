using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SayHello.Subscription.Public.Catalog;
using SayHello.Subscription.Public.Entitlements;
using SayHello.Subscription.Public.Subscriptions;
using SayHello.Subscription.Entitlements;
using Shouldly;
using Xunit;

namespace SayHello.Subscription.Public;

public class PublicContractsTests
{
    [Theory]
    [InlineData(-1, 20)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public void List_inputs_reject_unbounded_or_invalid_paging(int skip, int pageSize)
    {
        var catalog = new GetPublicCatalogInput { SkipCount = skip, MaxResultCount = pageSize };
        var mine = new GetMySubscriptionsInput { SkipCount = skip, MaxResultCount = pageSize };
        IsValid(catalog).ShouldBeFalse();
        IsValid(mine).ShouldBeFalse();
    }

    [Fact]
    public void Inputs_reject_unknown_enum_values_empty_identifiers_and_oversized_searches()
    {
        IsValid(new GetPublicCatalogInput { Sorting = (SubscriptionCatalogSort)999 }).ShouldBeFalse();
        IsValid(new GetPublicCatalogInput { ProductId = Guid.Empty }).ShouldBeFalse();
        IsValid(new GetPublicCatalogInput { Filter = new string('x', SubscriptionConsts.MaxNameLength + 1) })
            .ShouldBeFalse();
        IsValid(new GetMySubscriptionsInput { Sorting = (UserSubscriptionSort)999 }).ShouldBeFalse();
        IsValid(new GetMySubscriptionsInput { Status = (UserSubscriptionStatus)999 }).ShouldBeFalse();
        IsValid(new GetMySubscriptionsInput { ProductId = Guid.Empty }).ShouldBeFalse();
        IsValid(new GetMySubscriptionsInput { Filter = new string('x', SubscriptionConsts.MaxNameLength + 1) })
            .ShouldBeFalse();
    }

    [Fact]
    public void Current_user_contracts_never_accept_an_arbitrary_owner_or_tenant()
    {
        foreach (var contract in new[] { typeof(IMySubscriptionAppService), typeof(ICurrentUserEntitlementAppService) })
        {
            contract.GetMethods().SelectMany(m => m.GetParameters())
                .ShouldNotContain(p => p.Name == "userId" || p.Name == "tenantId");
            contract.GetMethods().ShouldAllBe(m => m.Name.StartsWith("Get", StringComparison.Ordinal));
        }

        typeof(GetMySubscriptionsInput).GetProperties()
            .ShouldNotContain(p => p.Name == "UserId" || p.Name == "TenantId");
        typeof(GetPublicCatalogInput).GetProperties()
            .ShouldNotContain(p => p.Name == "State" || p.Name == "PublishedOnly" || p.Name == "TenantId");
    }

    [Fact]
    public void Default_plan_contract_is_not_an_assignment_and_legacy_summary_defaults_remain_compatible()
    {
        var summary = new EffectiveSubscriptionDto();
        summary.HasEffectiveSubscription.ShouldBeFalse();
        summary.Subscription.ShouldBeNull();
        summary.Source.ShouldBe(EntitlementSource.None);
        summary.PlanId.ShouldBeNull();
        summary.Entitlements.ShouldBeEmpty();
        summary.EntitlementsAreLive.ShouldBeFalse();
        new DefaultSubscriptionPlanDto().Source.ShouldBe(EntitlementSource.DefaultPlan);
        typeof(DefaultSubscriptionPlanDto).GetProperties().ShouldNotContain(property =>
            new[] { "SubscriptionId", "UserId", "TenantId", "AssignmentId", "StartsAt", "ExpiresAt", "Status" }
                .Contains(property.Name));
        typeof(DefaultSubscriptionPlanDto).GetProperty(nameof(DefaultSubscriptionPlanDto.Source))!
            .CanWrite.ShouldBeFalse();
    }

    private static bool IsValid(object value) =>
        Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), true);
}
