using SayHello.Subscription.Admin.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace SayHello.Subscription.Admin.Permissions;

public class SubscriptionAdminPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(SubscriptionAdminPermissions.GroupName, L("Administration"));
        var root = group.AddPermission(SubscriptionAdminPermissions.Default, L("Administration"));
        foreach (var area in new[] { "Products", "Plans", "Bundles" })
        {
            var permission = root.AddChild(SubscriptionAdminPermissions.Default + "." + area, L(area));
            foreach (var action in new[] { "Create", "Update", "Delete", "Publish" })
            {
                permission.AddChild(permission.Name + "." + action, L(action));
            }
        }

        var users = root.AddChild(SubscriptionAdminPermissions.Users.Default, L("Users"));
        foreach (var action in new[] { "Lookup", "Assign", "Revoke", "AdjustExpiration" })
        {
            users.AddChild(users.Name + "." + action, L(action));
        }
    }

    private static LocalizableString L(string name) => LocalizableString.Create<SubscriptionAdminResource>(name);
}
