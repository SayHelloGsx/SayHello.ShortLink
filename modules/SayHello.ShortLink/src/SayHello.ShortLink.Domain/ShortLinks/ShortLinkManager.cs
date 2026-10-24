using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinkNamespaces;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinks;

public class ShortLinkManager : DomainService
{
    private readonly IShortLinkRepository _shortLinkRepository;
    private readonly ShortLinkDomainAccessPolicy _domainAccessPolicy;
    private readonly ShortLinkDefaultNamespaceProvider _defaultNamespaceProvider;
    private readonly IShortLinkNamespaceRepository _namespaceRepository;
    private readonly ShortLinkNamespaceLock _namespaceLock;
    private readonly IShortCodeGenerator _shortCodeGenerator;
    private readonly ShortCodePolicy _shortCodePolicy;
    private readonly ITargetUrlValidator _targetUrlValidator;
    private readonly ISettingProvider _settingProvider;
    private readonly IShortLinkCapabilityProvider _capabilityProvider;
    private readonly ShortLinkCreationLock _creationLock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ShortLinkManager(
        IShortLinkRepository shortLinkRepository,
        ShortLinkDomainAccessPolicy domainAccessPolicy,
        IShortCodeGenerator shortCodeGenerator,
        ShortCodePolicy shortCodePolicy,
        ITargetUrlValidator targetUrlValidator,
        ISettingProvider settingProvider,
        IShortLinkCapabilityProvider capabilityProvider,
        ShortLinkCreationLock creationLock,
        IUnitOfWorkManager unitOfWorkManager,
        ShortLinkDefaultNamespaceProvider defaultNamespaceProvider,
        IShortLinkNamespaceRepository namespaceRepository,
        ShortLinkNamespaceLock namespaceLock)
    {
        _shortLinkRepository = shortLinkRepository;
        _domainAccessPolicy = domainAccessPolicy;
        _defaultNamespaceProvider = defaultNamespaceProvider;
        _namespaceRepository = namespaceRepository;
        _namespaceLock = namespaceLock;
        _shortCodeGenerator = shortCodeGenerator;
        _shortCodePolicy = shortCodePolicy;
        _targetUrlValidator = targetUrlValidator;
        _settingProvider = settingProvider;
        _capabilityProvider = capabilityProvider;
        _creationLock = creationLock;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public virtual async Task<ShortLink> CreateAndSaveAsync(
        Guid id,
        Guid ownerUserId,
        string origin,
        string targetUrl,
        string? customCode,
        string? title,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default,
        Guid? namespaceId = null,
        bool useRootPath = false)
    {
        using var owned = _unitOfWorkManager.Current is null
            ? _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true)
            : null;
        var unit = owned ?? _unitOfWorkManager.Current!;
        ShortLinkUnitOfWorkLock.EnsureTransactional(unit);

        try
        {
            await _creationLock.AcquireAsync(unit, ownerUserId, cancellationToken);
            if (namespaceId.HasValue)
            {
                await _namespaceLock.AcquireAsync(namespaceId.Value, cancellationToken);
            }
            var shortLink = await CreateAsync(
                id, ownerUserId, origin, targetUrl, customCode, title, expiresAt, cancellationToken,
                namespaceId, useRootPath);
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
    public virtual async Task<ShortLink> CreateAsync(
        Guid id,
        Guid ownerUserId,
        string origin,
        string targetUrl,
        string? customCode,
        string? title,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default,
        Guid? namespaceId = null,
        bool useRootPath = false)
    {
        if (namespaceId.HasValue && useRootPath)
        {
            throw new BusinessException(ShortLinkErrorCodes.NamespaceSelectionConflict);
        }
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

        var isCustomCode = !customCode.IsNullOrWhiteSpace();
        if (isCustomCode)
        {
            var customCodeQuota = await _capabilityProvider.GetCustomCodeQuotaAsync(
                ownerUserId,
                cancellationToken);
            if (!customCodeQuota.IsGranted)
            {
                throw new BusinessException(
                    ShortLinkErrorCodes.CustomCodeQuotaNotGranted);
            }

            var currentCustomCodeCount =
                await _shortLinkRepository.GetCustomCodeCountByOwnerAsync(
                    ownerUserId,
                    cancellationToken);
            if (customCodeQuota.Limit is { } customCodeLimit &&
                currentCustomCodeCount >= customCodeLimit)
            {
                throw new BusinessException(
                        ShortLinkErrorCodes.CustomCodeQuotaExceeded)
                    .WithData("Limit", customCodeLimit);
            }
        }

        if (useRootPath)
        {
            var rootQuota = await _capabilityProvider.GetRootLinkQuotaAsync(ownerUserId, cancellationToken);
            if (!rootQuota.IsGranted)
            {
                throw new BusinessException(ShortLinkErrorCodes.RootLinkQuotaNotGranted);
            }
            var rootCount = await _shortLinkRepository.GetRootLinkCountByOwnerAsync(ownerUserId, cancellationToken);
            if (rootQuota.Limit is { } rootLimit && rootCount >= rootLimit)
            {
                throw new BusinessException(ShortLinkErrorCodes.RootLinkQuotaExceeded).WithData("Limit", rootLimit);
            }
        }

        var domain = await _domainAccessPolicy.GetAuthorizedAsync(
            ownerUserId,
            origin,
            cancellationToken);
        string pathNamespace;
        if (namespaceId.HasValue)
        {
            var namespaceQuota = await _capabilityProvider.GetNamespaceQuotaAsync(ownerUserId, cancellationToken);
            if (!namespaceQuota.IsEnabled)
            {
                throw new BusinessException(ShortLinkErrorCodes.NamespacesNotGranted);
            }
            var selected = await _namespaceRepository.FindAsync(namespaceId.Value, cancellationToken: cancellationToken);
            if (selected is null || selected.OwnerUserId != ownerUserId || selected.TenantId != CurrentTenant.Id ||
                selected.DomainId != domain.Id || !string.Equals(selected.Origin, domain.Origin, StringComparison.Ordinal))
            {
                throw new BusinessException(ShortLinkErrorCodes.NamespaceAccessDenied);
            }
            pathNamespace = selected.Name;
        }
        else
        {
            pathNamespace = useRootPath ? string.Empty : await _defaultNamespaceProvider.GetAsync(cancellationToken);
        }
        var validatedTarget = await _targetUrlValidator.ValidateAsync(
            targetUrl,
            cancellationToken);
        var code = customCode.IsNullOrWhiteSpace()
            ? await GenerateAvailableCodeAsync(domain.Origin, pathNamespace, cancellationToken)
            : await ValidateAvailableCustomCodeAsync(domain.Origin, pathNamespace, customCode!, cancellationToken);

        return new ShortLink(
            id,
            CurrentTenant.Id,
            ownerUserId,
            domain.Id,
            domain.Origin,
            code,
            validatedTarget.NormalizedUrl,
            title,
            expiresAt,
            isCustomCode,
            pathNamespace,
            namespaceId);
    }
    public virtual async Task<IReadOnlyList<ShortLinkDomain>> GetAvailableDomainsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _domainAccessPolicy.GetAvailableAsync(userId, cancellationToken);
    }

    public virtual async Task UpdateAsync(
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
        string pathNamespace,
        string customCode,
        CancellationToken cancellationToken)
    {
        var code = _shortCodePolicy.ValidateCustomCode(customCode, allowReserved: pathNamespace.Length > 0);
        if (await _shortLinkRepository.CodeExistsAsync(origin, pathNamespace, code, cancellationToken))
        {
            throw new BusinessException(ShortLinkErrorCodes.CodeAlreadyExists)
                .WithData("Code", code);
        }

        return code;
    }

    private async Task<string> GenerateAvailableCodeAsync(
        string origin,
        string pathNamespace,
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
            if ((pathNamespace.Length > 0 || !ShortLinkReservedCodes.Contains(code)) &&
                !await _shortLinkRepository.CodeExistsAsync(origin, pathNamespace, code, cancellationToken))
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
