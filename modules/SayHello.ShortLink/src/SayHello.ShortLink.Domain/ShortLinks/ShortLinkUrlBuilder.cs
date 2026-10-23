using System;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
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
        return Build(origin, string.Empty, code);
    }

    public string Build(string? origin, string pathNamespace, string code)
    {
        if (pathNamespace != string.Empty && !ShortLinkNamespaceFormat.IsValid(pathNamespace))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidNamespace);
        }

        string normalizedOrigin;
        try
        {
            normalizedOrigin = ShortLinkDomainOrigin.Normalize(origin ?? _options.BaseUrl);
        }
        catch (BusinessException)
        {
            throw new AbpException("The short-link Origin must be an absolute HTTP or HTTPS Origin.");
        }

        var baseUri = new Uri(normalizedOrigin + "/", UriKind.Absolute);
        var path = pathNamespace.Length == 0
            ? Uri.EscapeDataString(code)
            : $"{Uri.EscapeDataString(pathNamespace)}/{Uri.EscapeDataString(code)}";
        return new Uri(
            baseUri,
            path).AbsoluteUri;
    }
}
