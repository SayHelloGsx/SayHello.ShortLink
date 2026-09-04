using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.ShortLinks;

public class DnsHostAddressResolver : IHostAddressResolver, ITransientDependency
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        return await Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}
