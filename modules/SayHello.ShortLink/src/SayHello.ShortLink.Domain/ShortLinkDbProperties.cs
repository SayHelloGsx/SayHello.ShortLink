namespace SayHello.ShortLink;

public static class ShortLinkDbProperties
{
    public static string DbTablePrefix { get; set; } = "ShortLink";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "ShortLink";
}
