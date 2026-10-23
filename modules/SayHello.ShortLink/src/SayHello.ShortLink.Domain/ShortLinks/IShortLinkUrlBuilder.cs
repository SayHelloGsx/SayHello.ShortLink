namespace SayHello.ShortLink.ShortLinks;

public interface IShortLinkUrlBuilder
{
    string Build(string origin, string code);

    string Build(string code);

    string Build(string? origin, string pathNamespace, string code);
}
