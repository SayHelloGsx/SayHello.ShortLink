using System;
using System.Collections.Generic;

namespace SayHello.ShortLink.ShortLinks;

public sealed class ShortLinkDomainAccess
{
    private readonly HashSet<string> _origins;

    public static ShortLinkDomainAccess Unrestricted { get; } =
        new(false, Array.Empty<string>());

    public bool IsRestricted { get; }

    public IReadOnlySet<string> Origins => _origins;

    private ShortLinkDomainAccess(bool isRestricted, IEnumerable<string> origins)
    {
        IsRestricted = isRestricted;
        _origins = new HashSet<string>(origins, StringComparer.Ordinal);
    }

    public static ShortLinkDomainAccess Restricted(IEnumerable<string> origins)
    {
        ArgumentNullException.ThrowIfNull(origins);
        return new ShortLinkDomainAccess(true, origins);
    }

    public bool Allows(string origin) =>
        !IsRestricted || _origins.Contains(origin);
}
