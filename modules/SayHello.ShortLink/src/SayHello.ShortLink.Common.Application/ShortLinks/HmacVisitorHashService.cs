using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.Common.ShortLinks;

public class HmacVisitorHashService : IVisitorHashService, ITransientDependency
{
    private readonly byte[] _key;

    public HmacVisitorHashService(IOptions<ShortLinkPrivacyOptions> options)
    {
        var key = options.Value.VisitorHashKey;
        if (key.IsNullOrWhiteSpace() || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new AbpException("ShortLink:Privacy:VisitorHashKey must contain at least 32 UTF-8 bytes.");
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    public string Compute(string? ipAddress, DateTime visitedAt)
    {
        var normalizedAddress = IPAddress.TryParse(ipAddress, out var parsedAddress)
            ? parsedAddress.ToString()
            : "unknown";
        var value = $"{visitedAt.ToUniversalTime():yyyyMMdd}\n{normalizedAddress}";
        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}
