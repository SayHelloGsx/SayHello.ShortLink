using SayHello.ShortLink.ShortLinks;
using ShortLinkEntity = SayHello.ShortLink.ShortLinks.ShortLink;

namespace SayHello.ShortLink.Common.ShortLinks;

public static class ShortLinkDtoMapper
{
    public static ShortLinkDto ToDto(ShortLinkEntity shortLink, IShortLinkUrlBuilder urlBuilder)
    {
        return new ShortLinkDto
        {
            Id = shortLink.Id,
            TenantId = shortLink.TenantId,
            OwnerUserId = shortLink.OwnerUserId,
            Code = shortLink.Code,
            ShortUrl = urlBuilder.Build(shortLink.Code),
            TargetUrl = shortLink.TargetUrl,
            Title = shortLink.Title,
            Status = shortLink.Status,
            ExpiresAt = shortLink.ExpiresAt,
            TotalVisitCount = shortLink.TotalVisitCount,
            ConcurrencyStamp = shortLink.ConcurrencyStamp,
            CreationTime = shortLink.CreationTime,
            CreatorId = shortLink.CreatorId,
            LastModificationTime = shortLink.LastModificationTime,
            LastModifierId = shortLink.LastModifierId
        };
    }
}
