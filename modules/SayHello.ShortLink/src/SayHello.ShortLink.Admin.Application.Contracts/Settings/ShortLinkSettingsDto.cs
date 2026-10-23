using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.ShortLinkNamespaces;

namespace SayHello.ShortLink.Admin.Settings;

public class ShortLinkSettingsDto
{
    public bool IsQuotaExternallyManaged { get; set; }

    public int MaxLinksPerUser { get; set; }

    public int CreateLimitPerHour { get; set; }

    public int VisitRetentionDays { get; set; }

    public int DeletedCodeCooldownDays { get; set; }

    public int GeneratedCodeLength { get; set; }

    [Required]
    [StringLength(ShortLinkNamespaceConsts.MaxNameLength, MinimumLength = ShortLinkNamespaceConsts.MinNameLength)]
    [RegularExpression(@"\A[A-Za-z0-9_-]+\z")]
    public string DefaultNamespace { get; set; } = ShortLinkNamespaceConsts.DefaultNamespace;
}
