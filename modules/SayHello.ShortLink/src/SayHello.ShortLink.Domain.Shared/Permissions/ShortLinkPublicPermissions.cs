using Volo.Abp.Reflection;

namespace SayHello.ShortLink.Permissions;

public static class ShortLinkPublicPermissions
{
    public const string GroupName = "ShortLink.Public";
    public const string Default = GroupName + ".Links";
    public const string Create = Default + ".Create";
    public const string Update = Default + ".Update";
    public const string Delete = Default + ".Delete";
    public const string ViewStatistics = Default + ".ViewStatistics";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(ShortLinkPublicPermissions));
    }
}
