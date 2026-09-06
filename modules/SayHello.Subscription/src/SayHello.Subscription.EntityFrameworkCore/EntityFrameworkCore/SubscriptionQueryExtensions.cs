using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace SayHello.Subscription.EntityFrameworkCore;

internal static class SubscriptionQueryExtensions
{
    public static IOrderedQueryable<T> SortCatalog<T>(this IQueryable<T> query, SubscriptionCatalogSort sorting)
        where T : class
    {
        var sorted = sorting switch
        {
            SubscriptionCatalogSort.Name => query.OrderBy(x => EF.Property<string>(x, "Name")),
            SubscriptionCatalogSort.NameDescending => query.OrderByDescending(x => EF.Property<string>(x, "Name")),
            SubscriptionCatalogSort.Code => query.OrderBy(x => EF.Property<string>(x, "Code")),
            _ => query.OrderBy(x => EF.Property<int>(x, "DisplayOrder")).ThenBy(x => EF.Property<string>(x, "Name"))
        };
        return sorted.ThenBy(x => EF.Property<Guid>(x, "Id"));
    }
}
