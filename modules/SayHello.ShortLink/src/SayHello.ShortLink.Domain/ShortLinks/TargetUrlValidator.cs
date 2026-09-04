using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SayHello.ShortLink.BlockedDomains;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace SayHello.ShortLink.ShortLinks;

public class TargetUrlValidator : DomainService, ITargetUrlValidator
{
    private readonly IBlockedDomainRepository _blockedDomainRepository;
    private readonly IHostAddressResolver _hostAddressResolver;
    private readonly ShortLinkSecurityOptions _securityOptions;

    public TargetUrlValidator(
        IBlockedDomainRepository blockedDomainRepository,
        IHostAddressResolver hostAddressResolver,
        IOptions<ShortLinkSecurityOptions> securityOptions)
    {
        _blockedDomainRepository = blockedDomainRepository;
        _hostAddressResolver = hostAddressResolver;
        _securityOptions = securityOptions.Value;
    }

    public async Task<TargetUrlValidationResult> ValidateAsync(
        string targetUrl,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (targetUrl.IsNullOrWhiteSpace() ||
            targetUrl.Length > ShortLinkConsts.MaxTargetUrlLength ||
            !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !uri.UserInfo.IsNullOrEmpty() ||
            uri.Host.IsNullOrWhiteSpace())
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidTargetUrl);
        }

        string normalizedHost;
        try
        {
            normalizedHost = DomainNameNormalizer.Normalize(uri.IdnHost);
        }
        catch (ArgumentException)
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidTargetUrl);
        }

        if (IsOwnHost(normalizedHost) ||
            normalizedHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            normalizedHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ShortLinkErrorCodes.UnsafeTargetUrl);
        }

        if (await _blockedDomainRepository.IsBlockedAsync(normalizedHost, tenantId, cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.BlockedTargetDomain)
                .WithData("Host", normalizedHost);
        }

        if (IPAddress.TryParse(normalizedHost, out var literalAddress))
        {
            EnsurePublicAddress(literalAddress);
        }
        else
        {
            IReadOnlyList<IPAddress> addresses;
            try
            {
                addresses = await _hostAddressResolver.ResolveAsync(normalizedHost, cancellationToken);
            }
            catch (SocketException)
            {
                throw new BusinessException(ShortLinkErrorCodes.TargetHostCannotBeResolved)
                    .WithData("Host", normalizedHost);
            }

            if (addresses.Count == 0)
            {
                throw new BusinessException(ShortLinkErrorCodes.TargetHostCannotBeResolved)
                    .WithData("Host", normalizedHost);
            }

            foreach (var address in addresses)
            {
                EnsurePublicAddress(address);
            }
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = normalizedHost
        };

        if (uri.IsDefaultPort)
        {
            builder.Port = -1;
        }

        return new TargetUrlValidationResult(builder.Uri.AbsoluteUri, normalizedHost);
    }

    private bool IsOwnHost(string normalizedHost)
    {
        return _securityOptions.OwnHosts.Any(ownHost =>
            DomainNameNormalizer.IsSameOrSubdomainOf(
                normalizedHost,
                DomainNameNormalizer.Normalize(ownHost)));
    }

    private static void EnsurePublicAddress(IPAddress address)
    {
        if (!IsPublicAddress(address))
        {
            throw new BusinessException(ShortLinkErrorCodes.UnsafeTargetUrl);
        }
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(
                bytes[0] == 0 ||
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) ||
                bytes[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var isUniqueLocal = (bytes[0] & 0xFE) == 0xFC;
            var isDocumentation = bytes[0] == 0x20 &&
                                  bytes[1] == 0x01 &&
                                  bytes[2] == 0x0D &&
                                  bytes[3] == 0xB8;

            return !(
                address.IsIPv6LinkLocal ||
                address.IsIPv6Multicast ||
                address.IsIPv6SiteLocal ||
                isUniqueLocal ||
                isDocumentation);
        }

        return false;
    }
}
