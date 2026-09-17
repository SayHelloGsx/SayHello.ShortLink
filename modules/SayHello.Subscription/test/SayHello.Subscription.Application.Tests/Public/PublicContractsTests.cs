using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Public.Catalog;
using SayHello.Subscription.Public.Entitlements;
using SayHello.Subscription.Public.Subscriptions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Definitions;
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
    public void Application_contracts_never_expose_tenant_identifiers()
    {
        var contractAssemblies = new[]
        {
            typeof(SubscriptionPagedInput).Assembly,
            typeof(IProductAdminAppService).Assembly,
            typeof(ICurrentUserEntitlementAppService).Assembly
        }.Distinct();

        var exposedMembers = contractAssemblies
            .SelectMany(assembly => assembly.ExportedTypes)
            .SelectMany(type =>
                type.GetProperties()
                    .Where(property => property.Name.Equals("TenantId", StringComparison.OrdinalIgnoreCase))
                    .Select(property => $"{type.FullName}.{property.Name}")
                    .Concat(type.GetMethods().SelectMany(method => method.GetParameters()
                        .Where(parameter => parameter.Name?.Equals(
                            "tenantId",
                            StringComparison.OrdinalIgnoreCase) == true)
                        .Select(parameter => $"{type.FullName}.{method.Name}({parameter.Name})"))))
            .ToArray();

        exposedMembers.ShouldBeEmpty();
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

    [Fact]
    public void Typed_string_value_dtos_validate_shape_limits_and_canonicalize_through_the_mapper()
    {
        var enumDto = new EntitlementValueDto
        {
            Type = SubscriptionEntitlementType.Enum,
            StringValue = "pro"
        };
        IsValid(enumDto).ShouldBeTrue();
        SubscriptionDtoMapper.ToValue(enumDto).StringValue.ShouldBe("pro");

        var setDto = new EntitlementValueDto
        {
            Type = SubscriptionEntitlementType.StringSet,
            StringValues = new() { "us", "apac" }
        };
        IsValid(setDto).ShouldBeTrue();
        var roundTrip = SubscriptionDtoMapper.ToDto(SubscriptionDtoMapper.ToValue(setDto));
        roundTrip.StringValues.ShouldBe(new[] { "apac", "us" });
        IsValid(new EntitlementValueDto
        {
            Type = SubscriptionEntitlementType.StringSet,
            StringValues = new() { "duplicate", "duplicate" }
        }).ShouldBeFalse();
        IsValid(new EntitlementValueDto
        {
            Type = SubscriptionEntitlementType.Enum,
            StringValue = new string('x', SubscriptionConsts.MaxEntitlementStringLength + 1)
        }).ShouldBeFalse();
        IsValid(new EntitlementValueDto
        {
            Type = SubscriptionEntitlementType.Boolean,
            BooleanValue = true,
            StringValue = "unexpected"
        }).ShouldBeFalse();
    }

    private static bool IsValid(object value) =>
        Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), true);
}
