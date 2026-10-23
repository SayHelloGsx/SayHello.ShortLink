using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace SayHello.ShortLink.Public.ShortLinkNamespaces;

[Authorize(ShortLinkPublicPermissions.Namespaces.Default)]
[RemoteService(false)]
public class ShortLinkNamespaceAppService(
    IShortLinkNamespaceRepository repository,
    ShortLinkNamespaceManager manager,
    IShortLinkUserEligibilityChecker eligibilityChecker) : ShortLinkApplicationService, IShortLinkNamespaceAppService
{
    public virtual async Task<ListResultDto<ShortLinkNamespaceDto>> GetListAsync(string? origin = null)
    {
        var normalizedOrigin = origin is null ? null : ShortLinkDomainOrigin.Normalize(origin);
        var entities = await repository.GetListByOwnerAsync(
            CurrentUser.GetId(), normalizedOrigin, CancellationTokenProvider.Token);
        return new ListResultDto<ShortLinkNamespaceDto>(entities.Select(ToDto).ToList());
    }

    [Authorize(ShortLinkPublicPermissions.Namespaces.Create)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<ShortLinkNamespaceDto> CreateAsync(CreateShortLinkNamespaceDto input)
    {
        var ownerUserId = CurrentUser.GetId();
        await eligibilityChecker.EnsureEligibleAsync(ownerUserId);
        var entity = await manager.CreateAndSaveAsync(
            ownerUserId, input.Origin, input.Name, CancellationTokenProvider.Token);
        return ToDto(entity);
    }

    [Authorize(ShortLinkPublicPermissions.Namespaces.Delete)]
    [UnitOfWork(isTransactional: true)]
    public virtual Task DeleteAsync(Guid id, DeleteShortLinkNamespaceDto input)
    {
        return manager.DeleteAsync(
            id, CurrentUser.GetId(), input.ConcurrencyStamp, CancellationTokenProvider.Token);
    }

    private static ShortLinkNamespaceDto ToDto(ShortLinkNamespace entity) => new()
    {
        Id = entity.Id,
        DomainId = entity.DomainId,
        Origin = entity.Origin,
        Name = entity.Name,
        CreationTime = entity.CreationTime,
        ConcurrencyStamp = entity.ConcurrencyStamp
    };
}
