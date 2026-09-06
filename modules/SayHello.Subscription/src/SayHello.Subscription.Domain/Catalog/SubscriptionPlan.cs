using System;
using System.Collections.Generic;
using System.Linq;
using SayHello.Subscription.Definitions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class SubscriptionPlan : AuditedAggregateRoot<Guid>, IMultiTenant
{
    private readonly List<SubscriptionPlanEntitlement> _entitlements = new();

    public Guid? TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public SubscriptionCatalogState State { get; private set; }
    public IReadOnlyCollection<SubscriptionPlanEntitlement> Entitlements => _entitlements.AsReadOnly();

    protected SubscriptionPlan()
    {
    }

    public SubscriptionPlan(Guid id, SubscriptionProduct product, string code,
        string name, string? description = null, int displayOrder = 0)
        : base(SubscriptionGuard.Id(id, nameof(id)))
    {
        ArgumentNullException.ThrowIfNull(product);
        if (product.State == SubscriptionCatalogState.Archived)
        {
            throw new BusinessException(SubscriptionErrorCodes.CatalogUnavailable);
        }

        TenantId = product.TenantId;
        ProductId = product.Id;
        ProductCode = product.Code;
        Code = SubscriptionCode.Normalize(code);
        UpdateDetails(name, description, displayOrder);
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

    public void ReplaceEntitlements(ProductDefinition definition, IReadOnlyDictionary<string, EntitlementValue> values)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(values);
        EnsureDefinition(definition);
        var replacements = new List<SubscriptionPlanEntitlement>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            var feature = definition.GetFeature(key);
            feature.Validate(value);
            if (!keys.Add(feature.Key))
            {
                throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
            }

            replacements.Add(new SubscriptionPlanEntitlement(TenantId, Id, feature.Key, value));
        }

        _entitlements.RemoveAll(value => !keys.Contains(value.FeatureKey));
        foreach (var replacement in replacements)
        {
            var existing = _entitlements.SingleOrDefault(value => value.FeatureKey == replacement.FeatureKey);
            if (existing == null)
            {
                _entitlements.Add(replacement);
            }
            else
            {
                existing.SetValue(replacement.ToValue());
            }
        }
    }

    public void Publish(SubscriptionProduct product, ProductDefinition definition)
    {
        EnsureNotArchived();
        EnsureProduct(product);
        EnsureDefinition(definition);
        foreach (var value in _entitlements)
        {
            definition.GetFeature(value.FeatureKey).Validate(value.ToValue());
        }

        State = SubscriptionCatalogState.Published;
    }

    public void EnsureProduct(SubscriptionProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);
        SubscriptionGuard.SameTenant(TenantId, product.TenantId);
        if (product.Id != ProductId || product.Code != ProductCode ||
            product.State != SubscriptionCatalogState.Published)
        {
            throw new BusinessException(SubscriptionErrorCodes.CatalogUnavailable);
        }
    }

    public void Withdraw()
    {
        EnsureNotArchived();
        State = SubscriptionCatalogState.Withdrawn;
    }

    public void Archive() => State = SubscriptionCatalogState.Archived;

    private void EnsureDefinition(ProductDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Code != ProductCode)
        {
            throw new BusinessException(SubscriptionErrorCodes.UnknownProduct);
        }
    }

    private void EnsureNotArchived()
    {
        if (State == SubscriptionCatalogState.Archived)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidState);
        }
    }
}
