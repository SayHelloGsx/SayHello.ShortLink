namespace SayHello.ShortLink.ShortLinks;

public interface IShortCodeGenerator
{
    string Generate(int length);
}
