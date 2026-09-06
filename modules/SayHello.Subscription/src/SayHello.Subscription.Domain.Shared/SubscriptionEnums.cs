namespace SayHello.Subscription;

public enum SubscriptionCatalogState
{
    Draft = 0,
    Published = 1,
    Withdrawn = 2,
    Archived = 3
}

public enum SubscriptionEntitlementType
{
    Boolean = 0,
    Numeric = 1
}

public enum SubscriptionEndReason
{
    Replaced = 0,
    Revoked = 1
}

public enum UserSubscriptionStatus
{
    Active = 0,
    Expired = 1,
    Replaced = 2,
    Revoked = 3,
    NotStarted = 4
}

public enum EntitlementGrantStatus
{
    NoSubscription = 0,
    NotGranted = 1,
    Granted = 2
}

public enum SubscriptionCatalogSort
{
    DisplayOrder = 0,
    Name = 1,
    NameDescending = 2,
    Code = 3
}

public enum UserSubscriptionSort
{
    StartsAtDescending = 0,
    StartsAt = 1,
    ExpiresAt = 2
}
