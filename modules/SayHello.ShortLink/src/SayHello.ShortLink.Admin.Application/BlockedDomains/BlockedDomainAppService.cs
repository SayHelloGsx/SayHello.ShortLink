using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.Common.BlockedDomains;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Data;

namespace SayHello.ShortLink.Admin.BlockedDomains;

[Authorize(ShortLinkAdminPermissions.ManageBlockedDomains)]
public class BlockedDomainAppService : ShortLinkApplicationService, IBlockedDomainAppService
{
    private readonly IBlockedDomainRepository _repository;
    private readonly IBlockedDomainCache _blockedDomainCache;
    private readonly BlockedDomainCsvImporter _csvImporter;

    public BlockedDomainAppService(
        IBlockedDomainRepository repository,
        IBlockedDomainCache blockedDomainCache,
        BlockedDomainCsvImporter csvImporter)
    {
        _repository = repository;
        _blockedDomainCache = blockedDomainCache;
        _csvImporter = csvImporter;
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
        await InvalidateCacheAsync(entity.Domain);
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
        await InvalidateCacheAsync(entity.Domain);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(
            id,
            cancellationToken: CancellationTokenProvider.Token);
        await _repository.DeleteAsync(
            entity,
            autoSave: true,
            cancellationToken: CancellationTokenProvider.Token);
        await InvalidateCacheAsync(entity.Domain);
    }

    public async Task<BlockedDomainImportResultDto> ImportAsync(IRemoteStreamContent file)
    {
        var execution = await _csvImporter.ImportAsync(
            file,
            CurrentTenant.Id,
            CancellationTokenProvider.Token);

        if (execution.ImportedDomains.Count > 0)
        {
            await InvalidateCacheAsync(execution.ImportedDomains);
        }

        return execution.Result;
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

    private async Task InvalidateCacheAsync(string domain)
    {
        IReadOnlyCollection<string> domains = [domain];
        await InvalidateCacheAsync(domains);
    }

    private async Task InvalidateCacheAsync(IReadOnlyCollection<string> domains)
    {
        var tenantId = CurrentTenant.Id;
        await _blockedDomainCache.InvalidateManyAsync(
            domains,
            tenantId,
            CancellationTokenProvider.Token);

        CurrentUnitOfWork?.OnCompleted(() =>
            _blockedDomainCache.InvalidateManyAsync(
                domains,
                tenantId,
                CancellationToken.None));
    }
}
