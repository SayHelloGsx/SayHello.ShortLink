namespace SayHello.ShortLink.Public.ShortLinks;

public class ShortLinkQrCodeDto
{
    public string ContentType { get; set; } = "image/svg+xml";

    public string Content { get; set; } = string.Empty;
}
