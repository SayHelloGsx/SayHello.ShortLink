using System;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkUrlBuilder : IShortLinkUrlBuilder, ITransientDependency
{
    private readonly ShortLinkUrlOptions _options;

    public ShortLinkUrlBuilder(IOptions<ShortLinkUrlOptions> options)
    {
        _options = options.Value;
    }

    public string Build(string code)
    {
        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new AbpException("ShortLink:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        return new Uri(
            new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute),
            Uri.EscapeDataString(code)).AbsoluteUri;
    }
}
