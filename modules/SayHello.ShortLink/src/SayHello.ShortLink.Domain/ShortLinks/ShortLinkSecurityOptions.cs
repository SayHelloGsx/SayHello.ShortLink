using System;
using System.Collections.Generic;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkSecurityOptions
{
    public HashSet<string> OwnHosts { get; } = new(StringComparer.OrdinalIgnoreCase);
}
