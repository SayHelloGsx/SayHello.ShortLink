using System;
using System.Data;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkManager : DomainService
{
    private readonly IShortLinkRepository _shortLinkRepository;
    private readonly IShortLinkDomainRepository _shortLinkDomainRepository;
    private readonly IShortCodeGenerator _shortCodeGenerator;
    private readonly ShortCodePolicy _shortCodePolicy;
    private readonly ITargetUrlValidator _targetUrlValidator;
    private readonly ISettingProvider _settingProvider;
    private readonly IShortLinkCapabilityProvider _capabilityProvider;
    private readonly ShortLinkCreationLock _creationLock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;

    public ShortLinkManager(
        IShortLinkRepository shortLinkRepository,
        IShortLinkDomainRepository shortLinkDomainRepository,
        IShortCodeGenerator shortCodeGenerator,
        ShortCodePolicy shortCodePolicy,
        ITargetUrlValidator targetUrlValidator,
        ISettingProvider settingProvider,
        IShortLinkCapabilityProvider capabilityProvider,
        ShortLinkCreationLock creationLock,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant)
    {
        _shortLinkRepository = shortLinkRepository;
        _shortLinkDomainRepository = shortLinkDomainRepository;
        _shortCodeGenerator = shortCodeGenerator;
        _shortCodePolicy = shortCodePolicy;
        _targetUrlValidator = targetUrlValidator;
        _settingProvider = settingProvider;
        _capabilityProvider = capabilityProvider;
        _creationLock = creationLock;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
    }

    public virtual async Task<ShortLink> CreateAndSaveAsync(
        Guid id,
        Guid ownerUserId,
        string origin,
        string targetUrl,
        string? customCode,
        string? title,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        using var owned = _unitOfWorkManager.Current is null
            ? _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true)
            : null;
        var unit = owned ?? _unitOfWorkManager.Current!;
        if (!unit.Options.IsTransactional ||
            unit.Options.IsolationLevel is IsolationLevel.RepeatableRead or
                IsolationLevel.Snapshot or IsolationLevel.ReadUncommitted)
        {
            throw new AbpException("Short-link creation requires a transactional, read-committed or serializable unit of work.");
        }

        try
        {
            await _creationLock.AcquireAsync(unit, ownerUserId, cancellationToken);
            var shortLink = await CreateAsync(
                id, ownerUserId, origin, targetUrl, customCode, title, expiresAt, cancellationToken);
            await _shortLinkRepository.InsertAsync(shortLink, autoSave: true, cancellationToken);
            if (owned is not null)
            {
                await owned.CompleteAsync(cancellationToken);
            }

            return shortLink;
        }
        catch
        {
            await unit.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    // Constructs an entity only. Persist through CreateAndSaveAsync to serialize quota checks.
    public async Task<ShortLink> CreateAsync(
        Guid id,
        Guid ownerUserId,
        string origin,
        string targetUrl,
        string? customCode,
        string? title,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new BusinessException(ShortLinkErrorCodes.LinkAccessDenied);
        }

        var quota = await _capabilityProvider.GetQuotaAsync(ownerUserId, cancellationToken);
        if (!quota.IsGranted)
        {
            throw new BusinessException(ShortLinkErrorCodes.LinkQuotaNotGranted);
        }

        var currentCount = await _shortLinkRepository.GetCountByOwnerAsync(
            ownerUserId,
            cancellationToken);

        if (quota.Limit is { } limit && currentCount >= limit)
        {
            throw new BusinessException(ShortLinkErrorCodes.LinkQuotaExceeded)
                .WithData("Limit", limit);
        }

        var domain = await GetAuthorizedDomainAsync(
            ownerUserId,
            origin,
            cancellationToken);
        var validatedTarget = await _targetUrlValidator.ValidateAsync(
            targetUrl,
            cancellationToken);
        var code = customCode.IsNullOrWhiteSpace()
            ? await GenerateAvailableCodeAsync(domain.Origin, cancellationToken)
            : await ValidateAvailableCustomCodeAsync(domain.Origin, customCode!, cancellationToken);

        return new ShortLink(
            id,
            _currentTenant.Id,
            ownerUserId,
            domain.Id,
            domain.Origin,
            code,
            validatedTarget.NormalizedUrl,
            title,
            expiresAt);
    }
    public async Task<IReadOnlyList<ShortLinkDomain>> GetAvailableDomainsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainAccessDenied);
        }

        var domains = (await _shortLinkDomainRepository.GetListAsync(cancellationToken))
            .Where(x => x.IsEnabled)
            .ToList();
        if (domains.Count(x => x.IsDefault) != 1)
        {
            throw new BusinessException(ShortLinkErrorCodes.DefaultDomainRequired);
        }
        var access = await _capabilityProvider.GetDomainAccessAsync(
            userId,
            cancellationToken);
        return domains
            .Where(domain => domain.IsDefault || access.Allows(domain.Origin))
            .ToList();
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
            cancellationToken);

        shortLink.Update(validatedTarget.NormalizedUrl, title, expiresAt);
    }

    private async Task<string> ValidateAvailableCustomCodeAsync(
        string origin,
        string customCode,
        CancellationToken cancellationToken)
    {
        var code = _shortCodePolicy.ValidateCustomCode(customCode);
        if (await _shortLinkRepository.CodeExistsAsync(origin, code, cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.CodeAlreadyExists)
                .WithData("Code", code);
        }

        return code;
    }

    private async Task<string> GenerateAvailableCodeAsync(
        string origin,
        CancellationToken cancellationToken)
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
                !await _shortLinkRepository.CodeExistsAsync(origin, code, cancellationToken))
            {
                return code;
            }
        }

        throw new AbpException(
            $"Unable to generate an available short code after {ShortLinkConsts.MaxCodeGenerationAttempts} attempts.");
    }
    private async Task<ShortLinkDomain> GetAuthorizedDomainAsync(
        Guid userId,
        string origin,
        CancellationToken cancellationToken)
    {
        var normalizedOrigin = ShortLinkDomainOrigin.Normalize(origin);
        var domain = await _shortLinkDomainRepository.FindByOriginAsync(
            normalizedOrigin,
            enabledOnly: true,
            cancellationToken);
        if (domain is null)
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainAccessDenied)
                .WithData("Origin", normalizedOrigin);
        }

        if (domain.IsDefault)
        {
            return domain;
        }
        var access = await _capabilityProvider.GetDomainAccessAsync(
            userId,
            cancellationToken);
        if (!access.Allows(domain.Origin))
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainAccessDenied)
                .WithData("Origin", normalizedOrigin);
        }

        return domain;
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
