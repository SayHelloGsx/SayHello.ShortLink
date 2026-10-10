using System;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;

namespace SayHello.ShortLink.Common.ShortLinks;

public static class ShortLinkConcurrencyGuard
{
    public static void Apply(
        IHasConcurrencyStamp entity,
        string concurrencyStamp)
    {
        if (!string.Equals(entity.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
        {
            throw new AbpDbConcurrencyException();
        }

        entity.ConcurrencyStamp = concurrencyStamp;
    }
}
