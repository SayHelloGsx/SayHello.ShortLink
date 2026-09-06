using System;

namespace SayHello.Subscription.Users;

public sealed record SubscriptionUserData(
    Guid Id,
    Guid? TenantId,
    string UserName,
    string? Name,
    string? Surname,
    string? Email,
    bool IsActive);
