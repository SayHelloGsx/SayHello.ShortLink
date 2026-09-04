using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.Public.ShortLinks;

public interface IShortLinkUserEligibilityChecker
{
    Task EnsureEligibleAsync(Guid userId, CancellationToken cancellationToken = default);
}
