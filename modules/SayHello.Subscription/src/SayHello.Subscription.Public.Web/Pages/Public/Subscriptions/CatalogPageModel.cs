using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Public.Catalog;

namespace SayHello.Subscription.Public.Web.Pages.Public.Subscriptions;

public abstract class CatalogPageModel : SubscriptionPublicPageModel
{
    protected ISubscriptionCatalogAppService Catalog { get; }

    [BindProperty(SupportsGet = true)]
    public GetPublicCatalogInput Input { get; set; } = new() { MaxResultCount = 20 };

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    public List<SubscriptionProductDto> Products { get; } = new();
    public SubscriptionPager Pagination { get; protected set; } = new(0, 0, 20, null, null);

    protected CatalogPageModel(ISubscriptionCatalogAppService catalog)
    {
        Catalog = catalog;
    }

    protected async Task LoadProductsAsync()
    {
        var skip = 0;
        while (true)
        {
            var page = await Catalog.GetProductsAsync(new GetPublicCatalogInput
            {
                SkipCount = skip,
                MaxResultCount = SubscriptionConsts.MaxPageSize,
                Sorting = SubscriptionCatalogSort.Name
            });
            Products.AddRange(page.Items);
            skip += page.Items.Count;
            if (page.Items.Count == 0 || skip >= page.TotalCount)
            {
                break;
            }
        }
    }
}
