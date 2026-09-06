using System;
using System.Collections.Generic;
using System.Linq;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace SayHello.Subscription;

public static class SubscriptionDtoMapper
{
    public static EntitlementValueDto ToDto(EntitlementValue value) => new()
    {
        Type = value.Type,
        BooleanValue = value.BooleanValue,
        NumericValue = value.NumericValue,
        IsUnlimited = value.IsUnlimited
    };

    public static EntitlementValue ToValue(EntitlementValueDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return EntitlementValue.FromStorage(dto.Type, dto.BooleanValue, dto.NumericValue, dto.IsUnlimited);
    }

    public static IReadOnlyDictionary<string, EntitlementValue> ToValues(IEnumerable<EntitlementInputDto> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new Dictionary<string, EntitlementValue>(StringComparer.Ordinal);
        foreach (var item in values)
        {
            ArgumentNullException.ThrowIfNull(item);
            var key = SubscriptionCode.Normalize(item.FeatureKey, SubscriptionConsts.MaxFeatureKeyLength);
            if (!result.TryAdd(key, ToValue(item.Value)))
            {
                throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
            }
        }

        return result;
    }

    public static SubscriptionVersion? ToVersion(SubscriptionVersionDto? dto) =>
        dto == null ? null : new SubscriptionVersion(dto.SubscriptionId, dto.ConcurrencyStamp);

    public static EntitlementDto ToDto(EntitlementSnapshotData snapshot) => new()
    {
        FeatureKey = snapshot.FeatureKey,
        DisplayName = snapshot.DisplayName,
        Value = ToDto(snapshot.Value)
    };

    public static SubscriptionProductDto ToDto(SubscriptionProduct product) => new()
    {
        Id = product.Id,
        Code = product.Code,
        Name = product.Name,
        Description = product.Description,
        DisplayOrder = product.DisplayOrder
    };

    public static SubscriptionPlanDto ToDto(SubscriptionPlan plan, SubscriptionProduct product,
        Func<string, string> featureDisplayName)
    {
        SubscriptionGuard.SameTenant(plan.TenantId, product.TenantId);
        if (plan.ProductId != product.Id)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidAssignment);
        }

        return new SubscriptionPlanDto
        {
            Id = plan.Id,
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            DisplayOrder = plan.DisplayOrder,
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            Entitlements = plan.Entitlements.Select(value => new EntitlementDto
            {
                FeatureKey = value.FeatureKey,
                DisplayName = featureDisplayName(value.FeatureKey),
                Value = ToDto(value.ToValue())
            }).ToList()
        };
    }

    public static SubscriptionBundleDto ToDto(SubscriptionBundle bundle,
        IReadOnlyDictionary<Guid, SubscriptionPlanDto> plans) => new()
    {
        Id = bundle.Id,
        Code = bundle.Code,
        Name = bundle.Name,
        Description = bundle.Description,
        DisplayOrder = bundle.DisplayOrder,
        Items = bundle.Items.Select(item =>
        {
            if (!plans.TryGetValue(item.PlanId, out var plan) || plan.ProductId != item.ProductId)
            {
                throw new BusinessException(SubscriptionErrorCodes.InvalidBundle);
            }

            return new SubscriptionBundleItemDto
            {
                ProductId = plan.ProductId,
                ProductCode = plan.ProductCode,
                ProductName = plan.ProductName,
                PlanId = plan.Id,
                PlanCode = plan.Code,
                PlanName = plan.Name,
                Entitlements = plan.Entitlements.ToList()
            };
        }).ToList()
    };

    public static UserSubscriptionDto ToDto(UserSubscription subscription, DateTime now) => new()
    {
        Id = subscription.Id,
        ProductId = subscription.ProductId,
        SourcePlanId = subscription.SourcePlanId,
        SourceBundleId = subscription.SourceBundleId,
        AssignmentId = subscription.AssignmentId,
        ProductCode = subscription.ProductCode,
        ProductName = subscription.ProductName,
        PlanCode = subscription.PlanCode,
        PlanName = subscription.PlanName,
        BundleCode = subscription.BundleCode,
        BundleName = subscription.BundleName,
        StartsAt = subscription.StartsAt,
        ExpiresAt = subscription.ExpiresAt,
        EndedAt = subscription.EndedAt,
        EndReason = subscription.EndReason,
        EndReasonDetail = subscription.EndReasonDetail,
        IsCurrent = subscription.IsCurrent,
        Status = subscription.GetStatus(now),
        Entitlements = subscription.Entitlements.Select(value => ToDto(value.ToSnapshot())).ToList()
    };

    public static BooleanEntitlementResultDto ToDto(BooleanEntitlementResult result) => new()
    {
        Status = result.Status,
        SubscriptionId = result.SubscriptionId,
        IsGranted = result.IsGranted
    };

    public static NumericEntitlementResultDto ToDto(NumericEntitlementResult result) => new()
    {
        Status = result.Status,
        SubscriptionId = result.SubscriptionId,
        IsGranted = result.IsGranted,
        Limit = result.Limit,
        IsUnlimited = result.IsUnlimited
    };

    public static PagedResultDto<TDto> ToPage<T, TDto>(SubscriptionPage<T> page, Func<T, TDto> map) =>
        new(page.TotalCount, page.Items.Select(map).ToList());
}
