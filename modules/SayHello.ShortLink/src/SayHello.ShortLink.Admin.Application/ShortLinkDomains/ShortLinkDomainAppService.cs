using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.ShortLinkDomains;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.Admin.ShortLinkDomains;

[Authorize(ShortLinkAdminPermissions.Domains.Default)]
public class ShortLinkDomainAppService :
    ShortLinkApplicationService,
    IShortLinkDomainAppService
{
    private readonly IShortLinkDomainRepository _repository;
    private readonly ShortLinkDomainManager _manager;
    private readonly ShortLinkDomainConfigurationLock _configurationLock;

    public ShortLinkDomainAppService(
        IShortLinkDomainRepository repository,
        ShortLinkDomainManager manager,
        ShortLinkDomainConfigurationLock configurationLock)
    {
        _repository = repository;
        _manager = manager;
        _configurationLock = configurationLock;
    }

    public async Task<ListResultDto<ShortLinkDomainDto>> GetListAsync()
    {
        var entities = await _repository.GetListAsync(CancellationTokenProvider.Token);
        var items = new List<ShortLinkDomainDto>(entities.Count);
        foreach (var entity in entities)
        {
            items.Add(await ToDtoAsync(entity));
        }

        return new ListResultDto<ShortLinkDomainDto>(items);
    }

    [Authorize(ShortLinkAdminPermissions.Domains.Create)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<ShortLinkDomainDto> CreateAsync(CreateShortLinkDomainDto input)
    {
        var cancellationToken = CancellationTokenProvider.Token;
        await _configurationLock.AcquireAsync(cancellationToken);
        var entity = await _manager.CreateAsync(
            input.Origin,
            cancellationToken);
        await _repository.InsertAsync(
            entity,
            autoSave: true,
            cancellationToken);
        return await ToDtoAsync(entity);
    }

    [Authorize(ShortLinkAdminPermissions.Domains.EnableDisable)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<ShortLinkDomainDto> SetEnabledAsync(
        Guid id,
        SetShortLinkDomainEnabledDto input)
    {
        var cancellationToken = CancellationTokenProvider.Token;
        await _configurationLock.AcquireAsync(cancellationToken);
        var entity = await _repository.GetAsync(
            id,
            cancellationToken: cancellationToken);
        if (input.IsEnabled)
        {
            entity.Enable();
        }
        else
        {
            entity.Disable();
        }

        await _repository.UpdateAsync(
            entity,
            autoSave: true,
            cancellationToken);
        return await ToDtoAsync(entity);
    }

    [Authorize(ShortLinkAdminPermissions.Domains.SetDefault)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task<ShortLinkDomainDto> SetDefaultAsync(Guid id)
    {
        var cancellationToken = CancellationTokenProvider.Token;
        await _configurationLock.AcquireAsync(cancellationToken);
        var entity = await _repository.GetAsync(
            id,
            cancellationToken: cancellationToken);
        if (entity.IsDefault)
        {
            return await ToDtoAsync(entity);
        }

        var currentDefault = await _manager.FindDefaultAsync(cancellationToken);
        if (currentDefault is not null)
        {
            _manager.ClearDefault(currentDefault);
            await _repository.UpdateAsync(
                currentDefault,
                autoSave: true,
                cancellationToken);
        }

        _manager.SetDefault(entity);
        await _repository.UpdateAsync(
            entity,
            autoSave: true,
            cancellationToken);
        return await ToDtoAsync(entity);
    }

    [Authorize(ShortLinkAdminPermissions.Domains.Delete)]
    [UnitOfWork(isTransactional: true)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var cancellationToken = CancellationTokenProvider.Token;
        string? origin = null;
        try
        {
            await _configurationLock.AcquireAsync(cancellationToken);
            var entity = await _repository.GetAsync(
                id,
                cancellationToken: cancellationToken);
            origin = entity.Origin;
            await _manager.EnsureCanDeleteAsync(entity, cancellationToken);
            await _repository.DeleteAsync(
                entity,
                autoSave: true,
                cancellationToken);
        }
        catch (Exception exception) when (IsForeignKeyViolation(exception))
        {
            throw new BusinessException(ShortLinkErrorCodes.DomainInUse)
                .WithData("Origin", origin ?? id.ToString("D"))
                .WithData("ReferenceCount", ">= 1");
        }
    }

    private async Task<ShortLinkDomainDto> ToDtoAsync(ShortLinkDomain entity)
    {
        return new ShortLinkDomainDto
        {
            Id = entity.Id,
            Origin = entity.Origin,
            IsEnabled = entity.IsEnabled,
            IsDefault = entity.IsDefault,
            ShortLinkReferenceCount =
                await _repository.GetShortLinkReferenceCountAsync(
                    entity.Id,
                    CancellationTokenProvider.Token),
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId
        };
    }

    internal static bool IsForeignKeyViolation(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is DbException databaseException &&
                (string.Equals(databaseException.SqlState, "23503", StringComparison.Ordinal) ||
                 databaseException.Message.Contains(
                     "FOREIGN KEY constraint failed",
                     StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
