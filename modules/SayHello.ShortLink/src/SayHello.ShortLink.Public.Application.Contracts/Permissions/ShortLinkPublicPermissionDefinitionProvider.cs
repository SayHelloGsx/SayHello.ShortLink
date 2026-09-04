using SayHello.ShortLink.Localization;
using SayHello.ShortLink.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace SayHello.ShortLink.Public.Permissions;

public class ShortLinkPublicPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(
            ShortLinkPublicPermissions.GroupName,
            L("Permission:ShortLink"));
        var links = group.AddPermission(
            ShortLinkPublicPermissions.Default,
            L("Permission:Links"));
        links.AddChild(ShortLinkPublicPermissions.Create, L("Permission:Links.Create"));
        links.AddChild(ShortLinkPublicPermissions.Update, L("Permission:Links.Update"));
        links.AddChild(ShortLinkPublicPermissions.Delete, L("Permission:Links.Delete"));
        links.AddChild(
            ShortLinkPublicPermissions.ViewStatistics,
            L("Permission:Links.ViewStatistics"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ShortLinkResource>(name);
    }
}
