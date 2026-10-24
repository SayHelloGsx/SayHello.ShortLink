using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class SubscriptionBundleItem : Entity, IMultiTenant
{
    public virtual Guid? TenantId { get; private set; }
    public virtual Guid BundleId { get; private set; }
    public virtual Guid ProductId { get; private set; }
    public virtual Guid PlanId { get; private set; }

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

    internal virtual void SetPlanId(Guid planId) => PlanId = SubscriptionGuard.Id(planId, nameof(planId));
}
