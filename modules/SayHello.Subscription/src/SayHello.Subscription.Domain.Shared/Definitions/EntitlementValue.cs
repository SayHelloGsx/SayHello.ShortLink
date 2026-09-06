using Volo.Abp;

namespace SayHello.Subscription.Definitions;

public sealed record EntitlementValue
{
    public SubscriptionEntitlementType Type { get; }
    public bool? BooleanValue { get; }
    public long? NumericValue { get; }
    public bool IsUnlimited { get; }

    private EntitlementValue(SubscriptionEntitlementType type, bool? booleanValue, long? numericValue, bool isUnlimited)
    {
        Type = type;
        BooleanValue = booleanValue;
        NumericValue = numericValue;
        IsUnlimited = isUnlimited;
    }

    public static EntitlementValue Boolean(bool value) =>
        new(SubscriptionEntitlementType.Boolean, value, null, false);

    public static EntitlementValue Numeric(long value)
    {
        if (value < 0)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
        }

        return new EntitlementValue(SubscriptionEntitlementType.Numeric, null, value, false);
    }

    public static EntitlementValue Unlimited() =>
        new(SubscriptionEntitlementType.Numeric, null, null, true);

    public static EntitlementValue FromStorage(
        SubscriptionEntitlementType type, bool? booleanValue, long? numericValue, bool isUnlimited)
    {
        if (type == SubscriptionEntitlementType.Boolean && booleanValue.HasValue &&
            numericValue == null && !isUnlimited)
        {
            return Boolean(booleanValue.Value);
        }

        if (type == SubscriptionEntitlementType.Numeric && booleanValue == null)
        {
            if (isUnlimited && numericValue == null)
            {
                return Unlimited();
            }

            if (!isUnlimited && numericValue.HasValue)
            {
                return Numeric(numericValue.Value);
            }
        }

        throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
    }
}
