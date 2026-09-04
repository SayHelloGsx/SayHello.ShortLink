using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.Common.ShortLinks;

public interface IShortLinkCacheInvalidator
{
    Task RemoveAsync(string code, CancellationToken cancellationToken = default);
}
