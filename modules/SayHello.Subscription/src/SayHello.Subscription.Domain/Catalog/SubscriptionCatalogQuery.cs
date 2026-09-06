using System;
using Volo.Abp;

namespace SayHello.Subscription.Catalog;

/// <summary>
/// Repositories validate this input before querying and use Id as a sorting tie-breaker.
/// PublishedOnly also requires published parent products and all published bundle components.
/// ProductId selects a product, a plan's product, or a bundle containing that product respectively.
/// </summary>
public sealed record SubscriptionCatalogQuery(
    Guid? TenantId,
    string? Filter = null,
    SubscriptionCatalogState? State = null,
    bool PublishedOnly = false,
    Guid? ProductId = null,
    SubscriptionCatalogSort Sorting = SubscriptionCatalogSort.DisplayOrder,
    int SkipCount = 0,
    int MaxResultCount = 20)
{
    public void Validate()
    {
        SubscriptionGuard.Paging(SkipCount, MaxResultCount);
        if (!Enum.IsDefined(Sorting) || (State.HasValue && !Enum.IsDefined(State.Value)) ||
            (Filter?.Length ?? 0) > SubscriptionConsts.MaxNameLength ||
            (PublishedOnly && State.HasValue && State != SubscriptionCatalogState.Published))
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidPaging);
        }
    }
}
