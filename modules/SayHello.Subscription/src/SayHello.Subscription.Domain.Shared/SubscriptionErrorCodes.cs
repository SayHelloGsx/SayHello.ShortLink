namespace SayHello.Subscription;

public static class SubscriptionErrorCodes
{
    public const string InvalidCode = "Subscription:InvalidCode";
    public const string UnknownProduct = "Subscription:UnknownProduct";
    public const string UnknownFeature = "Subscription:UnknownFeature";
    public const string InvalidEntitlementValue = "Subscription:InvalidEntitlementValue";
    public const string EntitlementTypeMismatch = "Subscription:EntitlementTypeMismatch";
    public const string InvalidState = "Subscription:InvalidState";
    public const string CatalogUnavailable = "Subscription:CatalogUnavailable";
    public const string DuplicateCode = "Subscription:DuplicateCode";
    public const string CatalogReferenced = "Subscription:CatalogReferenced";
    public const string InvalidBundle = "Subscription:InvalidBundle";
    public const string InvalidExpiration = "Subscription:InvalidExpiration";
    public const string UserNotFound = "Subscription:UserNotFound";
    public const string TenantMismatch = "Subscription:TenantMismatch";
    public const string ConcurrencyConflict = "Subscription:ConcurrencyConflict";
    public const string InvalidAssignment = "Subscription:InvalidAssignment";
    public const string NoEffectiveSubscription = "Subscription:NoEffectiveSubscription";
    public const string EntitlementNotGranted = "Subscription:EntitlementNotGranted";
    public const string InvalidPaging = "Subscription:InvalidPaging";
    public const string MutationLockUnavailable = "Subscription:MutationLockUnavailable";
}
