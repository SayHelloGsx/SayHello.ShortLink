using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.Common.BlockedDomains;
using SayHello.ShortLink.Common.ShortLinks;
using SayHello.ShortLink.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Data;

namespace SayHello.ShortLink.Admin.BlockedDomains;

[Authorize(ShortLinkAdminPermissions.ManageBlockedDomains)]
[RemoteService(false)]
public class BlockedDomainAppService : ShortLinkApplicationService, IBlockedDomainAppService
{
    private readonly IBlockedDomainRepository _repository;
    private readonly BlockedDomainManager _blockedDomainManager;
    private readonly IBlockedDomainCache _blockedDomainCache;
    private readonly BlockedDomainCsvImporter _csvImporter;

    public BlockedDomainAppService(
        IBlockedDomainRepository repository,
        BlockedDomainManager blockedDomainManager,
        IBlockedDomainCache blockedDomainCache,
        BlockedDomainCsvImporter csvImporter)
    {
        _repository = repository;
        _blockedDomainManager = blockedDomainManager;
        _blockedDomainCache = blockedDomainCache;
        _csvImporter = csvImporter;
    }

    public virtual async Task<ListResultDto<BlockedDomainDto>> GetListAsync()
    {
        var entities = await _repository.GetListAsync(
            CancellationTokenProvider.Token);

        return new ListResultDto<BlockedDomainDto>(entities.Select(ToDto).ToList());
    }

    public virtual async Task<BlockedDomainDto> CreateAsync(CreateBlockedDomainDto input)
    {
        var entity = await _blockedDomainManager.CreateAsync(
            input.Domain,
            input.Reason,
            CancellationTokenProvider.Token);

        await _repository.InsertAsync(
            entity,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await InvalidateCacheAsync(entity.Domain);
        return ToDto(entity);
    }

    public virtual async Task<BlockedDomainDto> UpdateAsync(Guid id, UpdateBlockedDomainDto input)
    {
        var entity = await _repository.GetAsync(
            id,
            cancellationToken: CancellationTokenProvider.Token);
        ShortLinkConcurrencyGuard.Apply(entity, input.ConcurrencyStamp);
        entity.Update(input.Reason, input.IsActive);
        await _repository.UpdateAsync(
            entity,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await InvalidateCacheAsync(entity.Domain);
        return ToDto(entity);
    }

    public virtual async Task DeleteAsync(Guid id, DeleteBlockedDomainDto input)
    {
        var entity = await _repository.GetAsync(
            id,
            cancellationToken: CancellationTokenProvider.Token);
        ShortLinkConcurrencyGuard.Apply(entity, input.ConcurrencyStamp);
        await _repository.DeleteAsync(
            entity,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await InvalidateCacheAsync(entity.Domain);
    }

    public virtual async Task<BlockedDomainImportResultDto> ImportAsync(IRemoteStreamContent file)
    {
        var execution = await _csvImporter.ImportAsync(
            file,
            CancellationTokenProvider.Token);

        if (execution.ImportedDomains.Count > 0)
        {
            await InvalidateCacheAsync(execution.ImportedDomains);
        }

        return execution.Result;
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

    private async Task InvalidateCacheAsync(string domain)
    {
        IReadOnlyCollection<string> domains = [domain];
        await InvalidateCacheAsync(domains);
    }

    private async Task InvalidateCacheAsync(IReadOnlyCollection<string> domains)
    {
        var snapshot = domains.ToArray();
        var tenantId = CurrentTenant.Id;
        if (CurrentUnitOfWork is null)
        {
            await _blockedDomainCache.InvalidateManyAsync(
                snapshot,
                CancellationTokenProvider.Token);
            return;
        }

        CurrentUnitOfWork.OnCompleted(async () =>
        {
            using (CurrentTenant.Change(tenantId))
            {
                await _blockedDomainCache.InvalidateManyAsync(
                    snapshot,
                    CancellationToken.None);
            }
        });
    }
}
