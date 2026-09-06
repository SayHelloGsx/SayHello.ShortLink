using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using SayHello.Subscription.Admin.Localization;

namespace SayHello.Subscription.Admin;

internal static class AdminValidation
{
    internal static ValidationResult Error(ValidationContext context, string message, params string[] members)
    {
        var localizer = context.GetService(typeof(IStringLocalizer<SubscriptionAdminResource>))
            as IStringLocalizer<SubscriptionAdminResource>;
        return new ValidationResult(localizer == null ? message : localizer[message].Value, members);
    }
}
