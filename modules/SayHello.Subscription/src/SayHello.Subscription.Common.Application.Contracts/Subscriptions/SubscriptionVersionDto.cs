using System;
using System.ComponentModel.DataAnnotations;

namespace SayHello.Subscription.Subscriptions;

public class SubscriptionVersionDto
{
    public Guid SubscriptionId { get; set; }

    [Required]
    [StringLength(SubscriptionConsts.MaxConcurrencyStampLength)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
