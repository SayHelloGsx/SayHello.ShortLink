using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class ShortLinkNamespacePolicy(IOptions<ShortLinkNamespaceRouteOptions> options) : ITransientDependency
{
    public string Validate(string name)
    {
        if (!ShortLinkNamespaceFormat.IsValid(name))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidNamespace);
        }

        if (options.Value.ReservedPrefixes.Contains(name))
        {
            throw new BusinessException(ShortLinkErrorCodes.ReservedNamespace)
                .WithData("Namespace", name);
        }

        return name;
    }
}
