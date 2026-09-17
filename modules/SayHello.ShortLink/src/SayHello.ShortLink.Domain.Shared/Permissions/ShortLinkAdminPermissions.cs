using Volo.Abp.Reflection;

namespace SayHello.ShortLink.Permissions;

public static class ShortLinkAdminPermissions
{
    public const string GroupName = "ShortLink.Admin";
    public const string Default = GroupName;
    public const string ManageAllLinks = Default + ".ManageAllLinks";
    public const string ManageBlockedDomains = Default + ".ManageBlockedDomains";
    public const string ManageSettings = Default + ".ManageSettings";

    public static class Domains
    {
        public const string Default = ShortLinkAdminPermissions.Default + ".Domains";
        public const string Create = Default + ".Create";
        public const string EnableDisable = Default + ".EnableDisable";
        public const string SetDefault = Default + ".SetDefault";
        public const string Delete = Default + ".Delete";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(ShortLinkAdminPermissions));
    }
}
