using System;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.ShortLinkDomains;
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
        if (_options.BaseUrl.IsNullOrWhiteSpace())
        {
            throw new AbpException("ShortLink:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        return Build(_options.BaseUrl, code);
    }

    public string Build(string origin, string code)
    {
        string normalizedOrigin;
        try
        {
            normalizedOrigin = ShortLinkDomainOrigin.Normalize(origin);
        }
        catch (BusinessException)
        {
            throw new AbpException("The short-link Origin must be an absolute HTTP or HTTPS Origin.");
        }

        var baseUri = new Uri(normalizedOrigin + "/", UriKind.Absolute);
        return new Uri(
            baseUri,
            Uri.EscapeDataString(code)).AbsoluteUri;
    }
}
