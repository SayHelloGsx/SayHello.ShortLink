using System;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class ShortLinkNamespaceManager(
    IShortLinkNamespaceRepository repository,
    IShortLinkNamespaceReservationRepository reservationRepository,
    ShortLinkNamespacePolicy policy,
    ShortLinkDomainAccessPolicy domainAccessPolicy,
    IShortLinkCapabilityProvider capabilityProvider,
    ShortLinkNamespaceLock namespaceLock,
    ShortLinkDomainConfigurationLock domainConfigurationLock,
    ShortLinkCreationLock creationLock,
    IUnitOfWorkManager unitOfWorkManager) : DomainService
{
    public virtual async Task<ShortLinkNamespace> CreateAndSaveAsync(
        Guid ownerUserId, string origin, string name, CancellationToken cancellationToken = default)
    {
        policy.Validate(name);
        if (ownerUserId == Guid.Empty)
        {
            throw new BusinessException(ShortLinkErrorCodes.NamespaceAccessDenied);
        }

        using var owned = unitOfWorkManager.Current is null
            ? unitOfWorkManager.Begin(requiresNew: true, isTransactional: true)
            : null;
        var unit = owned ?? unitOfWorkManager.Current!;
        ShortLinkUnitOfWorkLock.EnsureTransactional(unit);
        try
        {
            await namespaceLock.AcquireConfigurationAsync(cancellationToken);
            await domainConfigurationLock.AcquireAsync(cancellationToken);
            await creationLock.AcquireAsync(unit, ownerUserId, cancellationToken);
            var quota = await capabilityProvider.GetNamespaceQuotaAsync(ownerUserId, cancellationToken);
            if (!quota.IsEnabled)
            {
                throw new BusinessException(ShortLinkErrorCodes.NamespacesNotGranted);
            }
            var count = await repository.GetCountByOwnerAsync(ownerUserId, cancellationToken);
            if (quota.Limit is { } limit && count >= limit)
            {
                throw new BusinessException(ShortLinkErrorCodes.NamespaceQuotaExceeded).WithData("Limit", limit);
            }

            var domain = await domainAccessPolicy.GetAuthorizedAsync(ownerUserId, origin, cancellationToken);
            if (await reservationRepository.IsReservedAsync(name, cancellationToken))
            {
                throw new BusinessException(ShortLinkErrorCodes.ReservedNamespace).WithData("Namespace", name);
            }
            if (await repository.NameExistsAsync(domain.Origin, name, cancellationToken))
            {
                throw new BusinessException(ShortLinkErrorCodes.NamespaceAlreadyExists).WithData("Namespace", name);
            }

            var entity = new ShortLinkNamespace(
                GuidGenerator.Create(), CurrentTenant.Id, ownerUserId, domain.Id, domain.Origin, name);
            await repository.InsertAsync(entity, autoSave: true, cancellationToken);
            if (owned is not null)
            {
                await owned.CompleteAsync(cancellationToken);
            }
            return entity;
        }
        catch
        {
            await unit.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public virtual async Task DeleteAsync(
        Guid id, Guid ownerUserId, string concurrencyStamp, CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(concurrencyStamp, nameof(concurrencyStamp));
        using var owned = unitOfWorkManager.Current is null
            ? unitOfWorkManager.Begin(requiresNew: true, isTransactional: true)
            : null;
        var unit = owned ?? unitOfWorkManager.Current!;
        ShortLinkUnitOfWorkLock.EnsureTransactional(unit);
        try
        {
            await creationLock.AcquireAsync(unit, ownerUserId, cancellationToken);
            await namespaceLock.AcquireAsync(id, cancellationToken);
            var entity = await repository.FindAsync(id, cancellationToken: cancellationToken);
            if (ownerUserId == Guid.Empty || entity is null || entity.OwnerUserId != ownerUserId ||
                entity.TenantId != CurrentTenant.Id)
            {
                throw new BusinessException(ShortLinkErrorCodes.NamespaceAccessDenied);
            }
            if (await repository.HasLinkReferencesAsync(id, cancellationToken))
            {
                throw new BusinessException(ShortLinkErrorCodes.NamespaceInUse).WithData("Namespace", entity.Name);
            }

            entity.ConcurrencyStamp = concurrencyStamp;
            await repository.DeleteAsync(entity, autoSave: true, cancellationToken);
            if (owned is not null)
            {
                await owned.CompleteAsync(cancellationToken);
            }
        }
        catch
        {
            await unit.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
