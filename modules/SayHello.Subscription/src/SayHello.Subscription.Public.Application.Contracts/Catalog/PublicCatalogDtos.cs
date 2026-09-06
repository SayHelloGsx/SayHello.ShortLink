using System;
using System.Collections.Generic;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Entitlements;

namespace SayHello.Subscription.Public.Catalog;

public class PublicEntitlementDto : EntitlementDto
{
}

public class PublicSubscriptionPlanDto : SubscriptionCatalogItemDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public List<PublicEntitlementDto> Entitlements { get; set; } = new();
}

public class PublicSubscriptionBundleDto : SubscriptionCatalogItemDto
{
    public List<PublicSubscriptionBundleItemDto> Items { get; set; } = new();
}

public class PublicSubscriptionBundleItemDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public List<PublicEntitlementDto> Entitlements { get; set; } = new();
}
