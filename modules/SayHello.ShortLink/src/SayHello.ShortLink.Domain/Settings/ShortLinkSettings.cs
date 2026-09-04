namespace SayHello.ShortLink.Settings;

public static class ShortLinkSettings
{
    public const string GroupName = "ShortLink";

    public const string MaxLinksPerUser = GroupName + ".MaxLinksPerUser";
    public const string CreateLimitPerHour = GroupName + ".CreateLimitPerHour";
    public const string VisitRetentionDays = GroupName + ".VisitRetentionDays";
    public const string DeletedCodeCooldownDays = GroupName + ".DeletedCodeCooldownDays";
    public const string GeneratedCodeLength = GroupName + ".GeneratedCodeLength";
}
