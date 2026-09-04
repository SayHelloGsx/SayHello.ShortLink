using SayHello.ShortLink.WebHost.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace SayHello.ShortLink.WebHost.Permissions;

public class WebHostPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(WebHostPermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(WebHostPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<WebHostResource>(name);
    }
}
