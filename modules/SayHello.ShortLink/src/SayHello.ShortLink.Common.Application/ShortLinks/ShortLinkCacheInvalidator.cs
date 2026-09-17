using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.Common.ShortLinks;

public class ShortLinkCacheInvalidator : IShortLinkCacheInvalidator, ITransientDependency
{
    private readonly IDistributedCache<ShortLinkResolutionCacheItem, string> _cache;
    private readonly ShortLinkUrlOptions _options;

    public ShortLinkCacheInvalidator(
        IDistributedCache<ShortLinkResolutionCacheItem, string> cache,
        IOptions<ShortLinkUrlOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public Task RemoveAsync(
        string? origin,
        string code,
        CancellationToken cancellationToken = default)
    {
        return _cache.RemoveAsync(
            ShortLinkResolutionCacheKey.Create(origin ?? _options.BaseUrl, code),
            token: cancellationToken);
    }
}
