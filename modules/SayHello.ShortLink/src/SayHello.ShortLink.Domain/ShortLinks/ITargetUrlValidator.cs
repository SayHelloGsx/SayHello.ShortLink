using System;
using System.Threading;
using System.Threading.Tasks;

namespace SayHello.ShortLink.ShortLinks;

public interface ITargetUrlValidator
{
    Task<TargetUrlValidationResult> ValidateAsync(
        string targetUrl,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
