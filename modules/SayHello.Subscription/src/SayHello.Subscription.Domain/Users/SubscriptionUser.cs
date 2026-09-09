using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Users;

namespace SayHello.Subscription.Users;

public class SubscriptionUser : AggregateRoot<Guid>, IUser
{
    public Guid? TenantId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public string? Surname { get; private set; }
    public bool IsActive { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool PhoneNumberConfirmed { get; private set; }

    protected SubscriptionUser()
    {
    }

    public SubscriptionUser(IUserData user)
        : base(GetId(user))
    {
        TenantId = user.TenantId;
        UserName = user.UserName;
        Email = user.Email;
        Name = user.Name;
        Surname = user.Surname;
        IsActive = user.IsActive;
        EmailConfirmed = user.EmailConfirmed;
        PhoneNumber = user.PhoneNumber;
        PhoneNumberConfirmed = user.PhoneNumberConfirmed;
        if (user.ExtraProperties is { } properties)
        {
            foreach (var property in properties)
            {
                ExtraProperties[property.Key] = property.Value;
            }
        }
    }

    private static Guid GetId(IUserData user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return SubscriptionGuard.Id(user.Id, nameof(user.Id));
    }
}
