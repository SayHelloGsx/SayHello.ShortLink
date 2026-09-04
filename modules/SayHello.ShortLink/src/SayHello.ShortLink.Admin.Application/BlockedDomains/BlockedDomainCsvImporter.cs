using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Localization;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.Localization;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace SayHello.ShortLink.Admin.BlockedDomains;

public class BlockedDomainCsvImporter : ITransientDependency
{
    private readonly IBlockedDomainRepository _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IStringLocalizer<ShortLinkResource> _localizer;

    public BlockedDomainCsvImporter(
        IBlockedDomainRepository repository,
        IGuidGenerator guidGenerator,
        IStringLocalizer<ShortLinkResource> localizer)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
        _localizer = localizer;
    }

    public async Task<BlockedDomainCsvImportExecutionResult> ImportAsync(
        IRemoteStreamContent file,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(file);

        using var buffer = await ReadFileAsync(file, cancellationToken);
        var parsedRows = ParseRows(buffer);
        var distinctCandidates = new Dictionary<string, ImportCandidate>(
            StringComparer.OrdinalIgnoreCase);
        var result = new BlockedDomainImportResultDto
        {
            TotalRows = parsedRows.Count
        };

        foreach (var row in parsedRows)
        {
            if (!TryNormalize(row, result, out var candidate))
            {
                continue;
            }

            if (!distinctCandidates.TryAdd(candidate.Domain, candidate))
            {
                result.DuplicateCount++;
                result.Issues.Add(
                    CreateIssue(
                        row,
                        BlockedDomainImportIssueType.DuplicateInFile,
                        _localizer["BlockedDomainImport:DuplicateInFile"]));
            }
        }

        var existingDomains = (await _repository.GetExistingDomainsAsync(
                distinctCandidates.Keys.ToList(),
                tenantId,
                cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entities = new List<BlockedDomain>();

        foreach (var candidate in distinctCandidates.Values)
        {
            if (existingDomains.Contains(candidate.Domain))
            {
                result.ExistingCount++;
                result.Issues.Add(
                    new BlockedDomainImportIssueDto
                    {
                        RowNumber = candidate.RowNumber,
                        Domain = candidate.RawDomain,
                        Type = BlockedDomainImportIssueType.AlreadyExists,
                        Message = _localizer["BlockedDomainImport:AlreadyExists"]
                    });
                continue;
            }

            entities.Add(
                new BlockedDomain(
                    _guidGenerator.Create(),
                    tenantId,
                    candidate.Domain,
                    candidate.Reason));
        }

        if (entities.Count > 0)
        {
            await _repository.InsertManyAsync(
                entities,
                autoSave: true,
                cancellationToken: cancellationToken);
        }

        result.ImportedCount = entities.Count;
        return new BlockedDomainCsvImportExecutionResult(
            result,
            entities.Select(x => x.Domain).ToList());
    }

    private static void ValidateFile(IRemoteStreamContent file)
    {
        if (file.ContentLength == 0)
        {
            throw new BusinessException(ShortLinkErrorCodes.BlockedDomainImportEmpty);
        }

        if (file.ContentLength > BlockedDomainImportConsts.MaxFileSize)
        {
            throw new BusinessException(ShortLinkErrorCodes.BlockedDomainImportTooLarge);
        }

        if (!string.Equals(
                Path.GetExtension(file.FileName),
                ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ShortLinkErrorCodes.BlockedDomainImportInvalidExtension);
        }
    }

    private static async Task<MemoryStream> ReadFileAsync(
        IRemoteStreamContent file,
        CancellationToken cancellationToken)
    {
        var output = new MemoryStream();
        try
        {
            using var input = file.GetStream();
            var buffer = new byte[81920];
            long totalBytes = 0;

            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalBytes += read;
                if (totalBytes > BlockedDomainImportConsts.MaxFileSize)
                {
                    throw new BusinessException(ShortLinkErrorCodes.BlockedDomainImportTooLarge);
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (totalBytes == 0)
            {
                throw new BusinessException(ShortLinkErrorCodes.BlockedDomainImportEmpty);
            }

            output.Position = 0;
            return output;
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    private static List<RawImportRow> ParseRows(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);
            using var csv = new CsvReader(
                reader,
                new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    IgnoreBlankLines = true,
                    TrimOptions = TrimOptions.Trim,
                    HeaderValidated = null,
                    MissingFieldFound = null
                });

            if (!csv.Read() || !csv.ReadHeader())
            {
                throw new BusinessException(ShortLinkErrorCodes.BlockedDomainImportEmpty);
            }

            var headers = csv.HeaderRecord ?? [];
            var domainIndex = Array.FindIndex(
                headers,
                header => string.Equals(
                    header?.Trim(),
                    "Domain",
                    StringComparison.OrdinalIgnoreCase));
            if (domainIndex < 0)
            {
                throw new BusinessException(
                    ShortLinkErrorCodes.BlockedDomainImportMissingDomainHeader);
            }

            var reasonIndex = Array.FindIndex(
                headers,
                header => string.Equals(
                    header?.Trim(),
                    "Reason",
                    StringComparison.OrdinalIgnoreCase));
            var rows = new List<RawImportRow>();

            while (csv.Read())
            {
                if (rows.Count >= BlockedDomainImportConsts.MaxDataRows)
                {
                    throw new BusinessException(
                        ShortLinkErrorCodes.BlockedDomainImportTooManyRows);
                }

                rows.Add(
                    new RawImportRow(
                        rows.Count + 2,
                        csv.GetField(domainIndex),
                        reasonIndex >= 0 ? csv.GetField(reasonIndex) : null));
            }

            return rows;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is CsvHelperException or DecoderFallbackException)
        {
            throw new BusinessException(
                    ShortLinkErrorCodes.BlockedDomainImportInvalidCsv,
                    innerException: exception);
        }
    }

    private bool TryNormalize(
        RawImportRow row,
        BlockedDomainImportResultDto result,
        out ImportCandidate candidate)
    {
        candidate = default!;
        var rawDomain = row.Domain?.Trim();
        var reason = row.Reason?.Trim();

        if (rawDomain.IsNullOrWhiteSpace())
        {
            AddInvalidIssue(row, result, _localizer["BlockedDomainImport:DomainRequired"]);
            return false;
        }

        string normalizedDomain;
        try
        {
            normalizedDomain = DomainNameNormalizer.Normalize(rawDomain);
        }
        catch (ArgumentException)
        {
            AddInvalidIssue(row, result, _localizer["BlockedDomainImport:InvalidDomain"]);
            return false;
        }

        if (Uri.CheckHostName(normalizedDomain) != UriHostNameType.Dns)
        {
            AddInvalidIssue(row, result, _localizer["BlockedDomainImport:InvalidDomain"]);
            return false;
        }

        if (reason?.Length > BlockedDomainConsts.MaxReasonLength)
        {
            AddInvalidIssue(row, result, _localizer["BlockedDomainImport:ReasonTooLong"]);
            return false;
        }

        candidate = new ImportCandidate(
            row.RowNumber,
            rawDomain,
            normalizedDomain,
            reason.IsNullOrWhiteSpace() ? null : reason);
        return true;
    }

    private static BlockedDomainImportIssueDto CreateIssue(
        RawImportRow row,
        BlockedDomainImportIssueType type,
        string message)
    {
        return new BlockedDomainImportIssueDto
        {
            RowNumber = row.RowNumber,
            Domain = row.Domain,
            Type = type,
            Message = message
        };
    }

    private static void AddInvalidIssue(
        RawImportRow row,
        BlockedDomainImportResultDto result,
        string message)
    {
        result.InvalidCount++;
        result.Issues.Add(CreateIssue(row, BlockedDomainImportIssueType.Invalid, message));
    }

    private sealed record RawImportRow(int RowNumber, string? Domain, string? Reason);

    private sealed record ImportCandidate(
        int RowNumber,
        string RawDomain,
        string Domain,
        string? Reason);
}
