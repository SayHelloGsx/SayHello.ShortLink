namespace SayHello.Subscription;

public static class SubscriptionConsts
{
    public const int MaxCodeLength = 64;
    public const int MaxFeatureKeyLength = 128;
    public const int MaxNameLength = 256;
    public const int MaxDescriptionLength = 2048;
    public const int MaxReasonLength = 1024;
    public const int MaxConcurrencyStampLength = 40;
    public const int MaxPageSize = 100;
    public const int MaxEntitlementStringLength = 512;
    public const int MaxEntitlementStringSetCount = 100;
    public const int MaxEntitlementStringSetStorageLength =
        (MaxEntitlementStringLength * 6 + 3) * MaxEntitlementStringSetCount + 1;
}
