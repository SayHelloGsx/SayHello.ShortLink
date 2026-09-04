using SayHello.ShortLink.Localization;
using SayHello.ShortLink.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace SayHello.ShortLink.Admin.Permissions;

public class ShortLinkAdminPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(
            ShortLinkAdminPermissions.GroupName,
            L("Permission:Administration"));
        var administration = group.AddPermission(
            ShortLinkAdminPermissions.Default,
            L("Permission:Administration"));
        administration.AddChild(
            ShortLinkAdminPermissions.ManageAllLinks,
            L("Permission:Administration.ManageAllLinks"));
        administration.AddChild(
            ShortLinkAdminPermissions.ManageBlockedDomains,
            L("Permission:Administration.ManageBlockedDomains"));
        administration.AddChild(
            ShortLinkAdminPermissions.ManageSettings,
            L("Permission:Administration.ManageSettings"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ShortLinkResource>(name);
    }
}
