using System.Collections.Generic;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public sealed class BlockedDomainCsvImportExecutionResult
{
    public BlockedDomainImportResultDto Result { get; }

    public IReadOnlyCollection<string> ImportedDomains { get; }

    public BlockedDomainCsvImportExecutionResult(
        BlockedDomainImportResultDto result,
        IReadOnlyCollection<string> importedDomains)
    {
        Result = result;
        ImportedDomains = importedDomains;
    }
}
