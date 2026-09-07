using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Public.Catalog;
using SayHello.Subscription.Public.Entitlements;
using SayHello.Subscription.Public.Subscriptions;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.Domain.Entities;

namespace SayHello.Subscription.Public.Web.Pages.Public.Subscriptions;

[Authorize]
public class MineModel : SubscriptionPublicPageModel
{
    private readonly IMySubscriptionAppService _subscriptions;
    private readonly ICurrentUserEntitlementAppService _entitlements;

    [BindProperty(SupportsGet = true)]
    public GetMySubscriptionsInput Input { get; set; } = new() { CurrentOnly = true, MaxResultCount = 20 };

    [BindProperty(SupportsGet = true)]
    public GetPublicCatalogInput DefaultInput { get; set; } = new() { MaxResultCount = 20 };

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    public IReadOnlyList<UserSubscriptionDto> Items { get; private set; } = [];
    public UserSubscriptionDto? Detail { get; private set; }
    public SubscriptionPager Pagination { get; private set; } = new(0, 0, 20, null, null);
    public IReadOnlyList<DefaultSubscriptionPlanDto> DefaultPlans { get; private set; } = [];
    public SubscriptionPager DefaultPagination { get; private set; } = new(0, 0, 20, null, null);

    public MineModel(IMySubscriptionAppService subscriptions, ICurrentUserEntitlementAppService entitlements)
    {
        _subscriptions = subscriptions;
        _entitlements = entitlements;
    }

    public string DefaultProductUrl(Guid? productId)
    {
        var query = Request.Query
            .Where(pair => !string.Equals(pair.Key, "DefaultInput.ProductId", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pair.Key, "DefaultInput.SkipCount", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => (string?)pair.Value.ToString());
        query["DefaultInput.ProductId"] = productId?.ToString() ?? string.Empty;
        return Request.Path + QueryString.Create(query);
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
                Detail = await _subscriptions.GetAsync(Id.Value);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
        }
        else
        {
            // GET binding can replace the initializer with the API input's history-inclusive default.
            if (!Request.Query.ContainsKey("Input.CurrentOnly") && !Request.Query.ContainsKey("CurrentOnly"))
            {
                Input.CurrentOnly = true;
            }

            var page = await _subscriptions.GetListAsync(Input);
            Items = page.Items;
            Pagination = Pager(page.TotalCount, Input);
            if (Input.CurrentOnly)
            {
                if (!Request.Query.ContainsKey("DefaultInput.ProductId"))
                {
                    DefaultInput.ProductId ??= Input.ProductId;
                }

                var defaults = await _entitlements.GetDefaultPlansAsync(DefaultInput);
                DefaultPlans = defaults.Items;
                DefaultPagination = Pager(defaults.TotalCount, DefaultInput, nameof(DefaultInput));
            }
        }

        return Page();
    }
}
