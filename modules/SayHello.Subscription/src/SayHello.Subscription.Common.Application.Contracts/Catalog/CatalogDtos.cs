using System;
using System.Collections.Generic;
using SayHello.Subscription.Entitlements;
using Volo.Abp.Application.Dtos;

namespace SayHello.Subscription.Catalog;

public abstract class SubscriptionCatalogItemDto : EntityDto<Guid>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

public class SubscriptionProductDto : SubscriptionCatalogItemDto
{
}

public class SubscriptionPlanDto : SubscriptionCatalogItemDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public List<EntitlementDto> Entitlements { get; set; } = new();
}

public class SubscriptionBundleDto : SubscriptionCatalogItemDto
{
    public List<SubscriptionBundleItemDto> Items { get; set; } = new();
}

public class SubscriptionBundleItemDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public List<EntitlementDto> Entitlements { get; set; } = new();
}
