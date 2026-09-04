using System.ComponentModel.DataAnnotations;
using SayHello.ShortLink.ShortLinks;

namespace SayHello.ShortLink.Public.ShortLinks;

public class RecordShortLinkVisitDto
{
    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(ShortLinkConsts.MaxTargetUrlLength)]
    public string? Referrer { get; set; }

    [StringLength(1024)]
    public string? UserAgent { get; set; }
}
