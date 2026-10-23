using System.Text.RegularExpressions;

namespace SayHello.ShortLink.ShortLinks;

public static partial class ShortCodeFormat
{
    [GeneratedRegex(@"\A[A-Za-z0-9_-]{3,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    public static bool IsValid(string? code) => code is not null && CodeRegex().IsMatch(code);
}
