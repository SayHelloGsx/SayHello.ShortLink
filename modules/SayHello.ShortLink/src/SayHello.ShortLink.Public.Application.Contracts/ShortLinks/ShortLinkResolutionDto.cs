using System;

namespace SayHello.ShortLink.Public.ShortLinks;

public class ShortLinkResolutionDto
{
    public ShortLinkResolutionStatus Status { get; set; }

    public Guid? ShortLinkId { get; set; }

    public string? TargetUrl { get; set; }

    public string? BlockedDomain { get; set; }

    public string? BlockedReason { get; set; }
}
