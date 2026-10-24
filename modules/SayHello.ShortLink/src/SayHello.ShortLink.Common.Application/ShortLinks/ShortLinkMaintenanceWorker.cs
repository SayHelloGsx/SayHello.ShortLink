using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace SayHello.ShortLink.Common.ShortLinks;

public class ShortLinkMaintenanceWorker : AsyncPeriodicBackgroundWorkerBase
{
    public ShortLinkMaintenanceWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 60 * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var repository = workerContext.ServiceProvider
            .GetRequiredService<IShortLinkMaintenanceRepository>();
        var settingProvider = workerContext.ServiceProvider.GetRequiredService<ISettingProvider>();
        var clock = workerContext.ServiceProvider.GetRequiredService<IClock>();
        var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var distributedLock = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();
        var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var tenantStore = workerContext.ServiceProvider.GetRequiredService<ITenantStore>();

        await using var handle = await distributedLock.TryAcquireAsync(
            "ShortLink:Maintenance",
            cancellationToken: workerContext.CancellationToken);
        if (handle is null)
        {
            return;
        }

        var now = clock.Now.ToUniversalTime();
        var tenantIds = new List<Guid?> { null };
        tenantIds.AddRange((await tenantStore.GetListAsync()).Select(tenant => (Guid?)tenant.Id));

        // The multi-tenant data filter only exposes the current scope, so run the maintenance once per host/tenant.
        foreach (var tenantId in tenantIds)
        {
            workerContext.CancellationToken.ThrowIfCancellationRequested();
            using (currentTenant.Change(tenantId))
            {
                await RunMaintenanceAsync(
                    repository,
                    settingProvider,
                    unitOfWorkManager,
                    now,
                    workerContext.CancellationToken);
            }
        }
    }

    protected virtual async Task RunMaintenanceAsync(
        IShortLinkMaintenanceRepository repository,
        ISettingProvider settingProvider,
        IUnitOfWorkManager unitOfWorkManager,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var retentionDays = await GetPositiveSettingAsync(
            settingProvider,
            ShortLinkSettings.VisitRetentionDays,
            ShortLinkDefaults.VisitRetentionDays);
        var cooldownDays = await GetPositiveSettingAsync(
            settingProvider,
            ShortLinkSettings.DeletedCodeCooldownDays,
            ShortLinkDefaults.DeletedCodeCooldownDays);

        using var unitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await repository.ArchiveVisitsBeforeAsync(
            now.AddDays(-retentionDays),
            cancellationToken);
        await repository.PurgeDeletedLinksBeforeAsync(
            now.AddDays(-cooldownDays),
            cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
    }

    private static async Task<int> GetPositiveSettingAsync(
        ISettingProvider settingProvider,
        string name,
        int defaultValue)
    {
        var value = await settingProvider.GetOrNullAsync(name);
        if (value is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0)
        {
            throw new AbpException($"Setting '{name}' must be a positive integer.");
        }

        return parsed;
    }
}
