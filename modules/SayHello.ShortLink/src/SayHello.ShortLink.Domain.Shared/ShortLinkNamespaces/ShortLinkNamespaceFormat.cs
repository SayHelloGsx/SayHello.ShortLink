using System.Text.RegularExpressions;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public static partial class ShortLinkNamespaceFormat
{
    [GeneratedRegex(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    public static bool IsValid(string? name)
    {
        return name is not null && NameRegex().IsMatch(name);
    }
}
