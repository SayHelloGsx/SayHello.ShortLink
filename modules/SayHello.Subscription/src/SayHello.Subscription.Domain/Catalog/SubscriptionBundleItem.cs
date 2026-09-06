using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class SubscriptionBundleItem : Entity, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid BundleId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid PlanId { get; private set; }

    protected SubscriptionBundleItem()
    {
    }

    internal SubscriptionBundleItem(Guid? tenantId, Guid bundleId, SubscriptionPlan plan)
    {
        TenantId = tenantId;
        BundleId = bundleId;
        ProductId = plan.ProductId;
        PlanId = plan.Id;
    }

    public override object[] GetKeys() => new object[] { BundleId, ProductId };

    internal void SetPlanId(Guid planId) => PlanId = SubscriptionGuard.Id(planId, nameof(planId));
}
