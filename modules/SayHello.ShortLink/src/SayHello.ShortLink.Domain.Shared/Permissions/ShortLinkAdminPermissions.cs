using Volo.Abp.Reflection;

namespace SayHello.ShortLink.Permissions;

public static class ShortLinkAdminPermissions
{
    public const string GroupName = "ShortLink.Admin";
    public const string Default = GroupName;
    public const string ManageAllLinks = Default + ".ManageAllLinks";
    public const string ManageBlockedDomains = Default + ".ManageBlockedDomains";
    public const string ManageSettings = Default + ".ManageSettings";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(ShortLinkAdminPermissions));
    }
}
