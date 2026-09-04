namespace SayHello.ShortLink.Common.ShortLinks;

public interface IVisitMetadataParser
{
    VisitMetadata Parse(string? referrer, string? userAgent);
}
