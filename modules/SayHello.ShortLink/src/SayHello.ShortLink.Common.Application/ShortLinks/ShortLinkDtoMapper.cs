using System;
using SayHello.ShortLink.ShortLinks;
using ShortLinkEntity = SayHello.ShortLink.ShortLinks.ShortLink;

namespace SayHello.ShortLink.Common.ShortLinks;

public static class ShortLinkDtoMapper
{
    public static ShortLinkDto ToPublicDto(
        ShortLinkEntity shortLink,
        IShortLinkUrlBuilder urlBuilder,
        ShortLinkStatisticsLevel statisticsLevel)
    {
        var dto = ToDto(shortLink, urlBuilder);
        if (statisticsLevel == ShortLinkStatisticsLevel.None)
        {
            dto.TotalVisitCount = null;
        }

        return dto;
    }

    public static ShortLinkDto ToDto(ShortLinkEntity shortLink, IShortLinkUrlBuilder urlBuilder)
    {
        return new ShortLinkDto
        {
            Id = shortLink.Id,
            OwnerUserId = shortLink.OwnerUserId,
            DomainId = shortLink.DomainId,
            Origin = shortLink.Origin,
            Code = shortLink.Code,
            ShortUrl = string.IsNullOrWhiteSpace(shortLink.Origin)
                ? urlBuilder.Build(shortLink.Code)
                : urlBuilder.Build(shortLink.Origin, shortLink.Code),
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
