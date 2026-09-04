using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using SayHello.ShortLink.Admin.BlockedDomains;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.Localization;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Guids;
using Xunit;

namespace SayHello.ShortLink.BlockedDomains;

public class BlockedDomainCsvImporterTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IBlockedDomainRepository _repository;
    private readonly BlockedDomainCsvImporter _importer;
    private readonly IGuidGenerator _guidGenerator;

    public BlockedDomainCsvImporterTests()
    {
        _repository = GetRequiredService<IBlockedDomainRepository>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _importer = new BlockedDomainCsvImporter(
            _repository,
            _guidGenerator,
            GetRequiredService<IStringLocalizer<ShortLinkResource>>());
    }

    [Fact]
    public async Task ImportAsync_Should_Import_Valid_Rows_And_Classify_Skipped_Rows()
    {
        await WithUnitOfWorkAsync(() =>
            _repository.InsertAsync(
                new BlockedDomain(
                    _guidGenerator.Create(),
                    null,
                    "existing.example",
                    "Existing"),
                autoSave: true));

        const string csv = """
            Domain,Reason
            new.example,"Spam, phishing
            and malware"
            NEW.EXAMPLE,Duplicate
            existing.example,Already there
            https://bad.example,Invalid
            bücher.example,International
            """;

        var execution = await WithUnitOfWorkAsync(() =>
            _importer.ImportAsync(CreateFile(csv), null));

        execution.Result.TotalRows.ShouldBe(5);
        execution.Result.ImportedCount.ShouldBe(2);
        execution.Result.DuplicateCount.ShouldBe(1);
        execution.Result.ExistingCount.ShouldBe(1);
        execution.Result.InvalidCount.ShouldBe(1);
        execution.Result.Issues.Select(x => x.Type).ShouldBe(
            [
                BlockedDomainImportIssueType.DuplicateInFile,
                BlockedDomainImportIssueType.Invalid,
                BlockedDomainImportIssueType.AlreadyExists
            ],
            ignoreOrder: true);

        var imported = await WithUnitOfWorkAsync(() => _repository.GetListAsync(null));
        imported.Select(x => x.Domain).ShouldContain("new.example");
        imported.Select(x => x.Domain).ShouldContain("xn--bcher-kva.example");
        var importedReason = imported.Single(x => x.Domain == "new.example").Reason;
        importedReason.ShouldNotBeNull();
        importedReason!.ShouldContain("Spam, phishing");
        importedReason.ShouldContain("and malware");
    }

    [Fact]
    public async Task ImportAsync_Should_Require_Domain_Header()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _importer.ImportAsync(CreateFile("Host,Reason\nexample.com,Test"), null));

        exception.Code.ShouldBe(ShortLinkErrorCodes.BlockedDomainImportMissingDomainHeader);
    }

    [Fact]
    public async Task ImportAsync_Should_Reject_Files_Over_One_Megabyte()
    {
        var content = new string('a', (int)BlockedDomainImportConsts.MaxFileSize + 1);
        using var file = CreateFile(content);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _importer.ImportAsync(file, null));

        exception.Code.ShouldBe(ShortLinkErrorCodes.BlockedDomainImportTooLarge);
    }

    [Fact]
    public async Task ImportAsync_Should_Reject_More_Than_Ten_Thousand_Data_Rows()
    {
        var csv = new StringBuilder("Domain,Reason\n");
        for (var index = 0; index <= BlockedDomainImportConsts.MaxDataRows; index++)
        {
            csv.Append("domain")
                .Append(index)
                .AppendLine(".example,Test");
        }

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _importer.ImportAsync(CreateFile(csv.ToString()), null));

        exception.Code.ShouldBe(ShortLinkErrorCodes.BlockedDomainImportTooManyRows);
    }

    private static RemoteStreamContent CreateFile(string content)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return new RemoteStreamContent(
            new MemoryStream(bytes),
            "blocked-domains.csv",
            "text/csv",
            bytes.Length);
    }
}
