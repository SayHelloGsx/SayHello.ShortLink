using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.ShortLinkDomains;

public class ShortLinkDomainDataSeedContributor :
    IDataSeedContributor,
    ITransientDependency
{
    private readonly ShortLinkDomainManager _manager;
    private readonly ShortLinkDomainConfigurationLock _configurationLock;
    private readonly IShortLinkDomainRepository _domains;
    private readonly IShortLinkRepository _shortLinks;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IConfiguration _configuration;

    public ILogger<ShortLinkDomainDataSeedContributor> Logger { get; set; } =
        NullLogger<ShortLinkDomainDataSeedContributor>.Instance;

    public ShortLinkDomainDataSeedContributor(
        ShortLinkDomainManager manager,
        ShortLinkDomainConfigurationLock configurationLock,
        IShortLinkDomainRepository domains,
        IShortLinkRepository shortLinks,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IConfiguration configuration)
    {
        _manager = manager;
        _configurationLock = configurationLock;
        _domains = domains;
        _shortLinks = shortLinks;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _configuration = configuration;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        using (_currentTenant.Change(context.TenantId))
        {
            if (_unitOfWorkManager.Current?.Options.IsTransactional == true)
            {
                await SeedCurrentTenantAsync(context);
                return;
            }

            await SeedInNewUnitOfWorkAsync(context);
        }
    }

    private async Task SeedInNewUnitOfWorkAsync(DataSeedContext context)
    {
        using (var unitOfWork = _unitOfWorkManager.Begin(
                   requiresNew: true,
                   isTransactional: true))
        {
            try
            {
                await SeedCurrentTenantAsync(context);
                await unitOfWork.CompleteAsync();
            }
            catch
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }

    private async Task SeedCurrentTenantAsync(DataSeedContext context)
    {
        await _configurationLock.AcquireAsync(CancellationToken.None);
        var defaultDomain = await _domains.FindDefaultAsync();
        if (defaultDomain is null)
        {
            if (await _domains.GetCountAsync() > 0)
            {
                throw new BusinessException(ShortLinkErrorCodes.DefaultDomainRequired);
            }

            var configuredOrigin = _configuration["ShortLink:Urls:BaseUrl"];
            if (string.IsNullOrWhiteSpace(configuredOrigin))
            {
                Logger.LogError(
                    "ShortLink:Urls:BaseUrl is missing. Tenant {TenantId} has no default short-link Origin.",
                    context.TenantId);
                return;
            }

            try
            {
                defaultDomain = await _manager.CreateAsync(configuredOrigin);
                await _domains.InsertAsync(defaultDomain, autoSave: true);
            }
            catch (BusinessException exception)
                when (exception.Code == ShortLinkErrorCodes.InvalidDomainOrigin)
            {
                Logger.LogError(
                    exception,
                    "ShortLink:Urls:BaseUrl is invalid. Tenant {TenantId} has no default short-link Origin.",
                    context.TenantId);
                return;
            }
        }
        var updated = await _shortLinks.BackfillDomainAsync(
            defaultDomain.Id,
            defaultDomain.Origin);
        if (updated > 0)
        {
            Logger.LogInformation(
                "Assigned {Count} legacy short links for tenant {TenantId} to default Origin {Origin}.",
                updated,
                context.TenantId,
                defaultDomain.Origin);
        }
    }
}
