using System;
using System.Linq;
using Volo.Abp;

namespace SayHello.Subscription;

public static class SubscriptionCode
{
    public static string Normalize(string value, int maxLength = SubscriptionConsts.MaxCodeLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidCode);
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > maxLength ||
            !char.IsAsciiLetterOrDigit(normalized[0]) ||
            normalized.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '.' && c != '_' && c != '-'))
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidCode);
        }

        return normalized;
    }
}
