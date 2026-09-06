using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class SubscriptionBundle : AuditedAggregateRoot<Guid>, IMultiTenant
{
    private readonly List<SubscriptionBundleItem> _items = new();

    public Guid? TenantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public SubscriptionCatalogState State { get; private set; }
    public IReadOnlyCollection<SubscriptionBundleItem> Items => _items.AsReadOnly();

    protected SubscriptionBundle()
    {
    }

    public SubscriptionBundle(Guid id, Guid? tenantId, string code, string name,
        IReadOnlyCollection<SubscriptionPlan> plans, string? description = null, int displayOrder = 0)
        : base(SubscriptionGuard.Id(id, nameof(id)))
    {
        TenantId = tenantId;
        Code = SubscriptionCode.Normalize(code);
        UpdateDetails(name, description, displayOrder);
        ReplaceItems(plans);
    }

    public void UpdateDetails(string name, string? description, int displayOrder)
    {
        EnsureNotArchived();
        var checkedName = SubscriptionGuard.Name(name);
        var checkedDescription = SubscriptionGuard.Description(description);
        Name = checkedName;
        Description = checkedDescription;
        DisplayOrder = displayOrder;
    }

    public void ReplaceItems(IReadOnlyCollection<SubscriptionPlan> plans)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(plans);
        if (plans.Count < 2 || plans.Any(p => p == null) ||
            plans.Select(p => p.ProductId).Distinct().Count() != plans.Count)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidBundle);
        }

        var replacements = new List<SubscriptionBundleItem>();
        foreach (var plan in plans)
        {
            SubscriptionGuard.SameTenant(TenantId, plan.TenantId);
            if (plan.State == SubscriptionCatalogState.Archived)
            {
                throw new BusinessException(SubscriptionErrorCodes.CatalogUnavailable);
            }

            replacements.Add(new SubscriptionBundleItem(TenantId, Id, plan));
        }

        var componentsChanged = _items.Count != replacements.Count ||
            _items.Any(item => !replacements.Any(replacement =>
                replacement.ProductId == item.ProductId && replacement.PlanId == item.PlanId));
        _items.RemoveAll(item => !replacements.Any(replacement => replacement.ProductId == item.ProductId));
        foreach (var replacement in replacements)
        {
            var existing = _items.SingleOrDefault(item => item.ProductId == replacement.ProductId);
            if (existing == null)
            {
                _items.Add(replacement);
            }
            else
            {
                existing.SetPlanId(replacement.PlanId);
            }
        }
        // A changed component list must pass publication validation again.
        if (componentsChanged && State == SubscriptionCatalogState.Published)
        {
            State = SubscriptionCatalogState.Draft;
        }
    }

    public void Publish(IReadOnlyCollection<SubscriptionPlan> plans, IReadOnlyCollection<SubscriptionProduct> products)
    {
        EnsureNotArchived();
        EnsureAvailableComponents(plans, products);
        State = SubscriptionCatalogState.Published;
    }

    public void EnsureAvailableComponents(IReadOnlyCollection<SubscriptionPlan> plans,
        IReadOnlyCollection<SubscriptionProduct> products)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(products);
        if (_items.Count < 2 || plans.Any(p => p == null) || products.Any(p => p == null) ||
            plans.Count != _items.Count || products.Count != _items.Count ||
            plans.Select(p => p.Id).Distinct().Count() != _items.Count ||
            products.Select(p => p.Id).Distinct().Count() != _items.Count)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidBundle);
        }

        foreach (var item in _items)
        {
            var plan = plans.SingleOrDefault(p => p.Id == item.PlanId && p.ProductId == item.ProductId);
            var product = products.SingleOrDefault(p => p.Id == item.ProductId);
            if (plan == null || product == null || plan.State != SubscriptionCatalogState.Published)
            {
                throw new BusinessException(SubscriptionErrorCodes.CatalogUnavailable);
            }

            SubscriptionGuard.SameTenant(TenantId, plan.TenantId);
            plan.EnsureProduct(product);
        }
    }

    public void Withdraw()
    {
        EnsureNotArchived();
        State = SubscriptionCatalogState.Withdrawn;
    }

    public void Archive() => State = SubscriptionCatalogState.Archived;

    private void EnsureNotArchived()
    {
        if (State == SubscriptionCatalogState.Archived)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidState);
        }
    }
}
