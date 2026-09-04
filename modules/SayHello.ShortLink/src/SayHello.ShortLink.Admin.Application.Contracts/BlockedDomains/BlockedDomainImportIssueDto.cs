namespace SayHello.ShortLink.Admin.BlockedDomains;

public class BlockedDomainImportIssueDto
{
    public int RowNumber { get; set; }

    public string? Domain { get; set; }

    public BlockedDomainImportIssueType Type { get; set; }

    public string Message { get; set; } = string.Empty;
}
