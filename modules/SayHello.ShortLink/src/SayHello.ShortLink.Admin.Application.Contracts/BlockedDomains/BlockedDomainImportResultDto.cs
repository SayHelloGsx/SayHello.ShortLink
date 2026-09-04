using System.Collections.Generic;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public class BlockedDomainImportResultDto
{
    public int TotalRows { get; set; }

    public int ImportedCount { get; set; }

    public int ExistingCount { get; set; }

    public int DuplicateCount { get; set; }

    public int InvalidCount { get; set; }

    public List<BlockedDomainImportIssueDto> Issues { get; set; } = [];
}
