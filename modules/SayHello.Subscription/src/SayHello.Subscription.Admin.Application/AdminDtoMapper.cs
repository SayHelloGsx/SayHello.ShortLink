using System;
using System.Linq;
using SayHello.Subscription.Admin.Catalog;
using SayHello.Subscription.Admin.Users;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;

namespace SayHello.Subscription.Admin;

public static class AdminDtoMapper
{
    public static AdminProductDto ToDto(SubscriptionProduct product) => new()
    {
        Id = product.Id, Code = product.Code, Name = product.Name, Description = product.Description,
        DisplayOrder = product.DisplayOrder, State = product.State, ConcurrencyStamp = product.ConcurrencyStamp
    };

    public static AdminPlanDto ToDto(SubscriptionPlan plan, SubscriptionPlanDto dto) => new()
    {
        Id = dto.Id, Code = dto.Code, Name = dto.Name, Description = dto.Description,
        DisplayOrder = dto.DisplayOrder, ProductId = dto.ProductId, ProductCode = dto.ProductCode,
        ProductName = dto.ProductName, Entitlements = dto.Entitlements,
        State = plan.State, ConcurrencyStamp = plan.ConcurrencyStamp
    };

    public static AdminBundleDto ToDto(SubscriptionBundle bundle, SubscriptionBundleDto dto) => new()
    {
        Id = dto.Id, Code = dto.Code, Name = dto.Name, Description = dto.Description,
        DisplayOrder = dto.DisplayOrder, Items = dto.Items, State = bundle.State,
        ConcurrencyStamp = bundle.ConcurrencyStamp
    };

    public static AdminUserSubscriptionDto ToDto(UserSubscription subscription, DateTime now)
    {
        var dto = SubscriptionDtoMapper.ToDto(subscription, now);
        return new AdminUserSubscriptionDto
        {
            Id = dto.Id, UserId = subscription.UserId, ConcurrencyStamp = subscription.ConcurrencyStamp,
            ProductId = dto.ProductId, SourcePlanId = dto.SourcePlanId, SourceBundleId = dto.SourceBundleId,
            AssignmentId = dto.AssignmentId, ProductCode = dto.ProductCode, ProductName = dto.ProductName,
            PlanCode = dto.PlanCode, PlanName = dto.PlanName, BundleCode = dto.BundleCode, BundleName = dto.BundleName,
            StartsAt = dto.StartsAt, ExpiresAt = dto.ExpiresAt, EndedAt = dto.EndedAt, EndReason = dto.EndReason,
            EndReasonDetail = dto.EndReasonDetail, IsCurrent = dto.IsCurrent, Status = dto.Status,
            Entitlements = dto.Entitlements
        };
    }

    public static SubscriptionAssignmentTarget ToTarget(AssignmentTargetDto dto) =>
        new(dto.ProductId, dto.PlanId, dto.ProductConcurrencyStamp, dto.PlanConcurrencyStamp,
            dto.ExpiresAt, SubscriptionDtoMapper.ToVersion(dto.ExpectedCurrent));

    public static AssignmentPreviewDto ToDto(SubscriptionAssignmentPreview preview) => new()
    {
        UserId = preview.UserId, BundleId = preview.BundleId, BundleConcurrencyStamp = preview.BundleConcurrencyStamp,
        Items = preview.Items.Select(item => new AssignmentPreviewItemDto
        {
            ProductId = item.ProductId, PlanId = item.PlanId, ProductName = item.ProductName,
            ProductCode = item.ProductCode, PlanName = item.PlanName, PlanCode = item.PlanCode,
            ProductConcurrencyStamp = item.ProductConcurrencyStamp, PlanConcurrencyStamp = item.PlanConcurrencyStamp,
            ExpectedCurrent = item.ExpectedCurrent == null ? null : new SubscriptionVersionDto
            {
                SubscriptionId = item.ExpectedCurrent.SubscriptionId,
                ConcurrencyStamp = item.ExpectedCurrent.ConcurrencyStamp
            },
            CurrentExpiresAt = item.CurrentExpiresAt,
            Entitlements = item.Entitlements.Select(SubscriptionDtoMapper.ToDto).ToList()
        }).ToList()
    };
}
