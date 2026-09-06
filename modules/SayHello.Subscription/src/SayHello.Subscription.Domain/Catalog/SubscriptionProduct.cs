using System;
using SayHello.Subscription.Definitions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.Catalog;

public class SubscriptionProduct : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public SubscriptionCatalogState State { get; private set; }

    protected SubscriptionProduct()
    {
    }

    public SubscriptionProduct(Guid id, Guid? tenantId, ProductDefinition definition,
        string name, string? description = null, int displayOrder = 0)
        : base(SubscriptionGuard.Id(id, nameof(id)))
    {
        ArgumentNullException.ThrowIfNull(definition);
        TenantId = tenantId;
        Code = definition.Code;
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

    public void Publish()
    {
        EnsureNotArchived();
        State = SubscriptionCatalogState.Published;
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
