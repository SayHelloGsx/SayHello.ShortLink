using System;
using Volo.Abp;
using Volo.Abp.Localization;

namespace SayHello.Subscription.Definitions;

public sealed class FeatureDefinition
{
    public string Key { get; }
    public ILocalizableString DisplayName { get; }
    public ILocalizableString? Description { get; }
    public SubscriptionEntitlementType Type { get; }
    public long? Maximum { get; }
    public bool AllowUnlimited { get; }

    public FeatureDefinition(string key, ILocalizableString displayName, SubscriptionEntitlementType type,
        long? maximum = null, bool allowUnlimited = false, ILocalizableString? description = null)
    {
        Key = SubscriptionCode.Normalize(key, SubscriptionConsts.MaxFeatureKeyLength);
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        if (!Enum.IsDefined(type) || maximum < 0 ||
            (type == SubscriptionEntitlementType.Boolean && (maximum.HasValue || allowUnlimited)))
        {
            throw new AbpException($"Invalid subscription feature definition: {Key}.");
        }

        Type = type;
        Maximum = maximum;
        AllowUnlimited = allowUnlimited;
        Description = description;
    }

    public void Validate(EntitlementValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Type != Type)
        {
            throw new BusinessException(SubscriptionErrorCodes.EntitlementTypeMismatch)
                .WithData("FeatureKey", Key);
        }

        if ((value.IsUnlimited && !AllowUnlimited) ||
            (value.NumericValue.HasValue && Maximum.HasValue && value.NumericValue.Value > Maximum.Value))
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue)
                .WithData("FeatureKey", Key);
        }
    }
}
