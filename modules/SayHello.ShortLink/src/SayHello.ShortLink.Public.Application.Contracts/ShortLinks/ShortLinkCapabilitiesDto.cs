using System.Collections.Generic;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.ShortLinks;

public class ShortLinkCapabilitiesDto
{
    public long UsedLinks { get; set; }

    public bool IsQuotaGranted { get; set; }

    public bool IsUnlimited { get; set; }

    public long? MaxLinks { get; set; }

    public long? RemainingLinks { get; set; }

    public long UsedCustomCodes { get; set; }

    public bool IsCustomCodeQuotaGranted { get; set; }

    public bool IsCustomCodeUnlimited { get; set; }

    public long? MaxCustomCodes { get; set; }

    public long? RemainingCustomCodes { get; set; }

    public bool CanUseCustomCode => IsCustomCodeQuotaGranted &&
        (IsCustomCodeUnlimited || RemainingCustomCodes > 0);

    public string DefaultNamespace { get; set; } = string.Empty;

    public bool NamespacesEnabled { get; set; }

    public long UsedNamespaces { get; set; }

    public bool IsNamespaceQuotaGranted { get; set; }

    public bool IsNamespaceUnlimited { get; set; }

    public long? MaxNamespaces { get; set; }

    public long? RemainingNamespaces { get; set; }

    public bool CanCreateNamespace => NamespacesEnabled && IsNamespaceQuotaGranted &&
        (IsNamespaceUnlimited || RemainingNamespaces > 0);

    public long UsedRootLinks { get; set; }

    public bool IsRootLinkQuotaGranted { get; set; }

    public bool IsRootLinkUnlimited { get; set; }

    public long? MaxRootLinks { get; set; }

    public long? RemainingRootLinks { get; set; }

    public bool CanCreateRootLink => IsRootLinkQuotaGranted &&
        (IsRootLinkUnlimited || RemainingRootLinks > 0);

    public ShortLinkStatisticsLevel StatisticsLevel { get; set; }

    public bool StatisticsEnabled => StatisticsLevel != ShortLinkStatisticsLevel.None;

    public List<ShortLinkDomainOptionDto> Domains { get; set; } = [];
}

public class ShortLinkDomainOptionDto
{
    public string Origin { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
