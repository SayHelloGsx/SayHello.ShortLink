using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.Public.ShortLinks;

public interface IShortLinkCreationRateLimiter
{
    Task EnsureAllowedAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
