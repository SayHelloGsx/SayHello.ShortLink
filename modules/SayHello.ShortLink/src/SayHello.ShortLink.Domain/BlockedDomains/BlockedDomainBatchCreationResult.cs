using System.Collections.Generic;

namespace SayHello.ShortLink.BlockedDomains;

public sealed record BlockedDomainBatchCreationResult(
    IReadOnlyList<BlockedDomain> Created,
    IReadOnlySet<string> ExistingDomains);
