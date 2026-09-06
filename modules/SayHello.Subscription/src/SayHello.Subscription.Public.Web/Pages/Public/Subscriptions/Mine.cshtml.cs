using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Public.Subscriptions;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.Domain.Entities;

namespace SayHello.Subscription.Public.Web.Pages.Public.Subscriptions;

[Authorize]
public class MineModel : SubscriptionPublicPageModel
{
    private readonly IMySubscriptionAppService _subscriptions;

    [BindProperty(SupportsGet = true)]
    public GetMySubscriptionsInput Input { get; set; } = new() { CurrentOnly = true, MaxResultCount = 20 };

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    public IReadOnlyList<UserSubscriptionDto> Items { get; private set; } = [];
    public UserSubscriptionDto? Detail { get; private set; }
    public SubscriptionPager Pagination { get; private set; } = new(0, 0, 20, null, null);

    public MineModel(IMySubscriptionAppService subscriptions)
    {
        _subscriptions = subscriptions;
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
            var page = await _subscriptions.GetListAsync(Input);
            Items = page.Items;
            Pagination = Pager(page.TotalCount, Input);
        }

        return Page();
    }
}
