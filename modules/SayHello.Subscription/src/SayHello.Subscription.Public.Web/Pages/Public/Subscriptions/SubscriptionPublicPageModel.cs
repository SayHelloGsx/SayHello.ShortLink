using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using SayHello.Subscription.Public.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace SayHello.Subscription.Public.Web.Pages.Public.Subscriptions;

public abstract class SubscriptionPublicPageModel : AbpPageModel
{
    protected SubscriptionPublicPageModel()
    {
        LocalizationResourceType = typeof(SubscriptionPublicResource);
    }

    protected SubscriptionPager Pager(long totalCount, SubscriptionPagedInput input)
    {
        string Url(int skip) => Request.Path + QueryString.Create(
            Request.Query.Where(p => !string.Equals(p.Key, "Input.SkipCount", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(p => p.Key, p => (string?)p.Value.ToString())
                .Append(new System.Collections.Generic.KeyValuePair<string, string?>(
                    "Input.SkipCount", skip.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        return new SubscriptionPager(totalCount, input.SkipCount, input.MaxResultCount,
            input.SkipCount > 0 ? Url(Math.Max(0, input.SkipCount - input.MaxResultCount)) : null,
            (long)input.SkipCount + input.MaxResultCount < totalCount &&
            input.SkipCount <= int.MaxValue - input.MaxResultCount ? Url(input.SkipCount + input.MaxResultCount) : null);
    }
}

public sealed record SubscriptionPager(long TotalCount, int SkipCount, int PageSize,
    string? PreviousUrl, string? NextUrl);
