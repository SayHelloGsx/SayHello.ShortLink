using System;
using Volo.Abp;

namespace SayHello.Subscription;

public static class SubscriptionGuard
{
    public static Guid Id(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidAssignment).WithData("Field", name);
        }

        return id;
    }

    public static void SameTenant(Guid? expected, Guid? actual)
    {
        if (expected != actual)
        {
            throw new BusinessException(SubscriptionErrorCodes.TenantMismatch);
        }
    }

    public static DateTime Utc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidExpiration);
        }

        return value;
    }

    public static void FutureExpiration(DateTime now, DateTime? expiresAt)
    {
        Utc(now);
        if (expiresAt.HasValue && (Utc(expiresAt.Value) <= now))
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidExpiration);
        }
    }

    public static string Name(string value) =>
        Check.NotNullOrWhiteSpace(value, nameof(value), SubscriptionConsts.MaxNameLength).Trim();

    public static string? Description(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Check.Length(value.Trim(), nameof(value), SubscriptionConsts.MaxDescriptionLength);

    public static string ConcurrencyStamp(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > SubscriptionConsts.MaxConcurrencyStampLength)
        {
            throw new BusinessException(SubscriptionErrorCodes.ConcurrencyConflict);
        }

        return value;
    }

    public static void Paging(int skipCount, int maxResultCount)
    {
        if (skipCount < 0 || maxResultCount < 1 || maxResultCount > SubscriptionConsts.MaxPageSize)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidPaging);
        }
    }
}
