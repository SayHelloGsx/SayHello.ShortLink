using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
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

        var retentionDays = await GetPositiveSettingAsync(
            settingProvider,
            ShortLinkSettings.VisitRetentionDays,
            ShortLinkDefaults.VisitRetentionDays);
        var cooldownDays = await GetPositiveSettingAsync(
            settingProvider,
            ShortLinkSettings.DeletedCodeCooldownDays,
            ShortLinkDefaults.DeletedCodeCooldownDays);
        var now = clock.Now.ToUniversalTime();

        using var unitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await repository.ArchiveVisitsBeforeAsync(
            now.AddDays(-retentionDays),
            workerContext.CancellationToken);
        await repository.PurgeDeletedLinksBeforeAsync(
            now.AddDays(-cooldownDays),
            workerContext.CancellationToken);
        await unitOfWork.CompleteAsync(workerContext.CancellationToken);
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
