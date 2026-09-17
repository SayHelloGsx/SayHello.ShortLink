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
        var domains = administration.AddChild(
            ShortLinkAdminPermissions.Domains.Default,
            L("Permission:Administration.Domains"));
        domains.AddChild(
            ShortLinkAdminPermissions.Domains.Create,
            L("Permission:Administration.Domains.Create"));
        domains.AddChild(
            ShortLinkAdminPermissions.Domains.EnableDisable,
            L("Permission:Administration.Domains.EnableDisable"));
        domains.AddChild(
            ShortLinkAdminPermissions.Domains.SetDefault,
            L("Permission:Administration.Domains.SetDefault"));
        domains.AddChild(
            ShortLinkAdminPermissions.Domains.Delete,
            L("Permission:Administration.Domains.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ShortLinkResource>(name);
    }
}
