using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.Common.ShortLinks;

public class ShortLinkCacheInvalidator : IShortLinkCacheInvalidator, ITransientDependency
{
    private readonly IDistributedCache<ShortLinkResolutionCacheItem, string> _cache;

    public ShortLinkCacheInvalidator(
        IDistributedCache<ShortLinkResolutionCacheItem, string> cache)
    {
        _cache = cache;
    }

    public Task RemoveAsync(string code, CancellationToken cancellationToken = default)
    {
        return _cache.RemoveAsync(code, token: cancellationToken);
    }
}
