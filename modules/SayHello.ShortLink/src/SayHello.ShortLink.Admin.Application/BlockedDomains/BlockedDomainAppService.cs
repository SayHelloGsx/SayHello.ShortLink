using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;

namespace SayHello.ShortLink.Admin.BlockedDomains;

[Authorize(ShortLinkAdminPermissions.ManageBlockedDomains)]
public class BlockedDomainAppService : ShortLinkApplicationService, IBlockedDomainAppService
{
    private readonly IBlockedDomainRepository _repository;

    public BlockedDomainAppService(IBlockedDomainRepository repository)
    {
        _repository = repository;
    }

    public async Task<ListResultDto<BlockedDomainDto>> GetListAsync()
    {
        var entities = await _repository.GetListAsync(
            CurrentTenant.Id,
            CancellationTokenProvider.Token);

        return new ListResultDto<BlockedDomainDto>(entities.Select(ToDto).ToList());
    }

    public async Task<BlockedDomainDto> CreateAsync(CreateBlockedDomainDto input)
    {
        var normalizedDomain = DomainNameNormalizer.Normalize(input.Domain);
        if (await _repository.ExistsAsync(
                normalizedDomain,
                CurrentTenant.Id,
                cancellationToken: CancellationTokenProvider.Token))
        {
            throw new BusinessException(ShortLinkErrorCodes.BlockedDomainAlreadyExists)
                .WithData("Host", normalizedDomain);
        }

        var entity = new BlockedDomain(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            normalizedDomain,
            input.Reason);

        await _repository.InsertAsync(
            entity,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        return ToDto(entity);
    }

    public async Task<BlockedDomainDto> UpdateAsync(Guid id, UpdateBlockedDomainDto input)
    {
        var entity = await _repository.GetAsync(
            id,
            cancellationToken: CancellationTokenProvider.Token);
        EnsureConcurrencyStamp(entity, input.ConcurrencyStamp);
        entity.Update(input.Reason, input.IsActive);
        await _repository.UpdateAsync(
            entity,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(
            id,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
    }

    private static void EnsureConcurrencyStamp(BlockedDomain entity, string concurrencyStamp)
    {
        if (!string.Equals(entity.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
        {
            throw new AbpDbConcurrencyException();
        }
    }

    private static BlockedDomainDto ToDto(BlockedDomain entity)
    {
        return new BlockedDomainDto
        {
            Id = entity.Id,
            Domain = entity.Domain,
            Reason = entity.Reason,
            IsActive = entity.IsActive,
            ConcurrencyStamp = entity.ConcurrencyStamp,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId
        };
    }
}
