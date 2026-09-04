namespace SayHello.ShortLink.Admin.Settings;

public class ShortLinkSettingsDto
{
    public int MaxLinksPerUser { get; set; }

    public int CreateLimitPerHour { get; set; }

    public int VisitRetentionDays { get; set; }

    public int DeletedCodeCooldownDays { get; set; }

    public int GeneratedCodeLength { get; set; }
}
