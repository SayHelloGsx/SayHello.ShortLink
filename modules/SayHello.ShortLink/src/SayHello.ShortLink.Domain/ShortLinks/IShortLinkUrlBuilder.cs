namespace SayHello.ShortLink.ShortLinks;

public interface IShortLinkUrlBuilder
{
    string Build(string code);
}
