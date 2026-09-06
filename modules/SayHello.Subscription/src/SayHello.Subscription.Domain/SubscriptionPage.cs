using System;
using System.Collections.Generic;
using System.Linq;

namespace SayHello.Subscription;

public sealed class SubscriptionPage<T>
{
    public long TotalCount { get; }
    public IReadOnlyList<T> Items { get; }

    public SubscriptionPage(long totalCount, IEnumerable<T> items)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount));
        }

        ArgumentNullException.ThrowIfNull(items);
        TotalCount = totalCount;
        Items = Array.AsReadOnly(items.ToArray());
    }
}
