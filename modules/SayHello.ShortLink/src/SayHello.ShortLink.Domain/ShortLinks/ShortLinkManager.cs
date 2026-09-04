using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkManager : DomainService
{
    private readonly IShortLinkRepository _shortLinkRepository;
    private readonly IShortCodeGenerator _shortCodeGenerator;
    private readonly ShortCodePolicy _shortCodePolicy;
    private readonly ITargetUrlValidator _targetUrlValidator;
    private readonly ISettingProvider _settingProvider;

    public ShortLinkManager(
        IShortLinkRepository shortLinkRepository,
        IShortCodeGenerator shortCodeGenerator,
        ShortCodePolicy shortCodePolicy,
        ITargetUrlValidator targetUrlValidator,
        ISettingProvider settingProvider)
    {
        _shortLinkRepository = shortLinkRepository;
        _shortCodeGenerator = shortCodeGenerator;
        _shortCodePolicy = shortCodePolicy;
        _targetUrlValidator = targetUrlValidator;
        _settingProvider = settingProvider;
    }

    public async Task<ShortLink> CreateAsync(
        Guid id,
        Guid? tenantId,
        Guid ownerUserId,
        string targetUrl,
        string? customCode,
        string? title,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        var maxLinks = await GetPositiveSettingAsync(
            ShortLinkSettings.MaxLinksPerUser,
            ShortLinkDefaults.MaxLinksPerUser);
        var currentCount = await _shortLinkRepository.GetCountByOwnerAsync(
            ownerUserId,
            tenantId,
            cancellationToken);

        if (currentCount >= maxLinks)
        {
            throw new BusinessException(ShortLinkErrorCodes.LinkQuotaExceeded)
                .WithData("Limit", maxLinks);
        }

        var validatedTarget = await _targetUrlValidator.ValidateAsync(
            targetUrl,
            tenantId,
            cancellationToken);
        var code = customCode.IsNullOrWhiteSpace()
            ? await GenerateAvailableCodeAsync(cancellationToken)
            : await ValidateAvailableCustomCodeAsync(customCode!, cancellationToken);

        return new ShortLink(
            id,
            tenantId,
            ownerUserId,
            code,
            validatedTarget.NormalizedUrl,
            title,
            expiresAt);
    }

    public async Task UpdateAsync(
        ShortLink shortLink,
        string targetUrl,
        string? title,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        var validatedTarget = await _targetUrlValidator.ValidateAsync(
            targetUrl,
            shortLink.TenantId,
            cancellationToken);

        shortLink.Update(validatedTarget.NormalizedUrl, title, expiresAt);
    }

    private async Task<string> ValidateAvailableCustomCodeAsync(
        string customCode,
        CancellationToken cancellationToken)
    {
        var code = _shortCodePolicy.ValidateCustomCode(customCode);
        if (await _shortLinkRepository.CodeExistsAsync(code, cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.CodeAlreadyExists)
                .WithData("Code", code);
        }

        return code;
    }

    private async Task<string> GenerateAvailableCodeAsync(CancellationToken cancellationToken)
    {
        var length = await GetPositiveSettingAsync(
            ShortLinkSettings.GeneratedCodeLength,
            ShortLinkConsts.GeneratedCodeLength);

        if (length is < ShortLinkConsts.MinCodeLength or > ShortLinkConsts.MaxCodeLength)
        {
            throw new AbpException(
                $"Setting '{ShortLinkSettings.GeneratedCodeLength}' must be between " +
                $"{ShortLinkConsts.MinCodeLength} and {ShortLinkConsts.MaxCodeLength}.");
        }

        for (var attempt = 0; attempt < ShortLinkConsts.MaxCodeGenerationAttempts; attempt++)
        {
            var code = _shortCodeGenerator.Generate(length);
            if (!ShortLinkReservedCodes.Contains(code) &&
                !await _shortLinkRepository.CodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new AbpException(
            $"Unable to generate an available short code after {ShortLinkConsts.MaxCodeGenerationAttempts} attempts.");
    }

    private async Task<int> GetPositiveSettingAsync(string name, int defaultValue)
    {
        var value = await _settingProvider.GetOrNullAsync(name);
        if (value is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedValue) ||
            parsedValue <= 0)
        {
            throw new AbpException($"Setting '{name}' must be a positive integer.");
        }

        return parsedValue;
    }
}
