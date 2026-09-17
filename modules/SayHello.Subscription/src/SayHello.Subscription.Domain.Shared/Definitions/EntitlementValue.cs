using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace SayHello.Subscription.Definitions;

public sealed record EntitlementValue
{
    private readonly string? _stringSetStorageValue;

    public SubscriptionEntitlementType Type { get; }
    public bool? BooleanValue { get; }
    public long? NumericValue { get; }
    public bool IsUnlimited { get; }
    public string? StringValue { get; }
    public IReadOnlyList<string>? StringValues =>
        _stringSetStorageValue == null
            ? null
            : new ReadOnlyCollection<string>(JsonSerializer.Deserialize<string[]>(_stringSetStorageValue)!);

    private EntitlementValue(SubscriptionEntitlementType type, bool? booleanValue, long? numericValue,
        bool isUnlimited, string? stringValue, string? stringSetStorageValue)
    {
        Type = type;
        BooleanValue = booleanValue;
        NumericValue = numericValue;
        IsUnlimited = isUnlimited;
        StringValue = stringValue;
        _stringSetStorageValue = stringSetStorageValue;
    }

    public static EntitlementValue Boolean(bool value) =>
        new(SubscriptionEntitlementType.Boolean, value, null, false, null, null);

    public static EntitlementValue Numeric(long value)
    {
        if (value < 0)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
        }

        return new EntitlementValue(SubscriptionEntitlementType.Numeric, null, value, false, null, null);
    }

    public static EntitlementValue Unlimited() =>
        new(SubscriptionEntitlementType.Numeric, null, null, true, null, null);

    public static EntitlementValue FromStorage(
        SubscriptionEntitlementType type, bool? booleanValue, long? numericValue, bool isUnlimited,
        string? stringValue = null, string? stringSetValue = null)
    {
        if (type == SubscriptionEntitlementType.Boolean && booleanValue.HasValue &&
            numericValue == null && !isUnlimited && stringValue == null && stringSetValue == null)
        {
            return Boolean(booleanValue.Value);
        }

        if (type == SubscriptionEntitlementType.Numeric && booleanValue == null &&
            stringValue == null && stringSetValue == null)
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

        if (type == SubscriptionEntitlementType.Enum && booleanValue == null && numericValue == null &&
            !isUnlimited && stringValue != null && stringSetValue == null)
        {
            return Enum(stringValue);
        }

        if (type == SubscriptionEntitlementType.StringSet && booleanValue == null && numericValue == null &&
            !isUnlimited && stringValue == null && stringSetValue != null)
        {
            if (stringSetValue.Length > SubscriptionConsts.MaxEntitlementStringSetStorageLength)
            {
                throw InvalidValue();
            }

            try
            {
                var values = JsonSerializer.Deserialize<string[]>(stringSetValue);
                return values == null ? throw InvalidValue() : StringSet(values);
            }
            catch (JsonException)
            {
                throw InvalidValue();
            }
        }

        throw new BusinessException(SubscriptionErrorCodes.InvalidEntitlementValue);
    }

    public static EntitlementValue FromValues(
        SubscriptionEntitlementType type, bool? booleanValue, long? numericValue, bool isUnlimited,
        string? stringValue = null, IEnumerable<string>? stringValues = null)
    {
        if (type == SubscriptionEntitlementType.StringSet && booleanValue == null && numericValue == null &&
            !isUnlimited && stringValue == null && stringValues != null)
        {
            return StringSet(stringValues);
        }

        if (stringValues != null)
        {
            throw InvalidValue();
        }

        return FromStorage(type, booleanValue, numericValue, isUnlimited, stringValue);
    }

    public static EntitlementValue Enum(string value) =>
        new(SubscriptionEntitlementType.Enum, null, null, false, NormalizeString(value), null);

    public static EntitlementValue StringSet(IEnumerable<string> values)
    {
        if (values == null)
        {
            throw InvalidValue();
        }

        var canonical = new List<string>();
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in values)
        {
            if (canonical.Count == SubscriptionConsts.MaxEntitlementStringSetCount)
            {
                throw InvalidValue();
            }

            var value = NormalizeString(item);
            if (!distinct.Add(value))
            {
                throw InvalidValue();
            }

            canonical.Add(value);
        }

        canonical.Sort(StringComparer.Ordinal);
        return new EntitlementValue(SubscriptionEntitlementType.StringSet, null, null, false, null,
            JsonSerializer.Serialize(canonical));
    }

    public string? ToStorageStringSet() => _stringSetStorageValue;

    public bool HasSameValueAs(EntitlementValue? other) =>
        other != null &&
        Type == other.Type &&
        BooleanValue == other.BooleanValue &&
        NumericValue == other.NumericValue &&
        IsUnlimited == other.IsUnlimited &&
        string.Equals(StringValue, other.StringValue, StringComparison.Ordinal) &&
        string.Equals(_stringSetStorageValue, other._stringSetStorageValue, StringComparison.Ordinal);

    private static string NormalizeString(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized) ||
            normalized.Length > SubscriptionConsts.MaxEntitlementStringLength)
        {
            throw InvalidValue();
        }

        return normalized;
    }

    private static BusinessException InvalidValue() =>
        new(SubscriptionErrorCodes.InvalidEntitlementValue);
}
