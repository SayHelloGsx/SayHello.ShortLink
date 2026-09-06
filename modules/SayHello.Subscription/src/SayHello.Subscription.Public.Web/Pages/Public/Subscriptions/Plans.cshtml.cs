using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Public.Catalog;
using Volo.Abp.Domain.Entities;

namespace SayHello.Subscription.Public.Web.Pages.Public.Subscriptions;

[AllowAnonymous]
public class PlansModel : CatalogPageModel
{
    public IReadOnlyList<PublicSubscriptionPlanDto> Items { get; private set; } = [];
    public PublicSubscriptionPlanDto? Detail { get; private set; }

    public PlansModel(ISubscriptionCatalogAppService catalog) : base(catalog)
    {
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(L["Validation:InvalidFilter"].Value);
        }

        if (Id.HasValue)
        {
            try
            {
                Detail = await Catalog.GetPlanAsync(Id.Value);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
        }
        else
        {
            var page = await Catalog.GetPlansAsync(Input);
            Items = page.Items;
            Pagination = Pager(page.TotalCount, Input);
            await LoadProductsAsync();
        }

        return Page();
    }
}
