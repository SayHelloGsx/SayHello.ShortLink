using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace SayHello.Subscription.Subscriptions;

public class SubscriptionVersionDto : IHasConcurrencyStamp
{
    public Guid SubscriptionId { get; set; }

    [Required]
    [StringLength(SubscriptionConsts.MaxConcurrencyStampLength)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
