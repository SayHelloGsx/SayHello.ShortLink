using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.Settings;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace SayHello.ShortLink.Admin.Settings;

[Authorize(ShortLinkAdminPermissions.ManageSettings)]
public class ShortLinkSettingsAppService :
    ApplicationService,
    IShortLinkSettingsAppService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;

    public ShortLinkSettingsAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
    }

    public async Task<ShortLinkSettingsDto> GetAsync()
    {
        return new ShortLinkSettingsDto
        {
            MaxLinksPerUser = await GetValueAsync(
                ShortLinkSettings.MaxLinksPerUser,
                ShortLinkDefaults.MaxLinksPerUser),
            CreateLimitPerHour = await GetValueAsync(
                ShortLinkSettings.CreateLimitPerHour,
                ShortLinkDefaults.CreateLimitPerHour),
            VisitRetentionDays = await GetValueAsync(
                ShortLinkSettings.VisitRetentionDays,
                ShortLinkDefaults.VisitRetentionDays),
            DeletedCodeCooldownDays = await GetValueAsync(
                ShortLinkSettings.DeletedCodeCooldownDays,
                ShortLinkDefaults.DeletedCodeCooldownDays),
            GeneratedCodeLength = await GetValueAsync(
                ShortLinkSettings.GeneratedCodeLength,
                ShortLinkConsts.GeneratedCodeLength)
        };
    }

    public async Task<ShortLinkSettingsDto> UpdateAsync(ShortLinkSettingsDto input)
    {
        ValidateRange(input.MaxLinksPerUser, 1, 100_000, ShortLinkSettings.MaxLinksPerUser);
        ValidateRange(input.CreateLimitPerHour, 1, 10_000, ShortLinkSettings.CreateLimitPerHour);
        ValidateRange(input.VisitRetentionDays, 1, 3_650, ShortLinkSettings.VisitRetentionDays);
        ValidateRange(
            input.DeletedCodeCooldownDays,
            1,
            3_650,
            ShortLinkSettings.DeletedCodeCooldownDays);
        ValidateRange(
            input.GeneratedCodeLength,
            ShortLinkConsts.MinCodeLength,
            ShortLinkConsts.MaxCodeLength,
            ShortLinkSettings.GeneratedCodeLength);

        await _settingManager.SetGlobalAsync(
            ShortLinkSettings.MaxLinksPerUser,
            input.MaxLinksPerUser.ToString(CultureInfo.InvariantCulture));
        await _settingManager.SetGlobalAsync(
            ShortLinkSettings.CreateLimitPerHour,
            input.CreateLimitPerHour.ToString(CultureInfo.InvariantCulture));
        await _settingManager.SetGlobalAsync(
            ShortLinkSettings.VisitRetentionDays,
            input.VisitRetentionDays.ToString(CultureInfo.InvariantCulture));
        await _settingManager.SetGlobalAsync(
            ShortLinkSettings.DeletedCodeCooldownDays,
            input.DeletedCodeCooldownDays.ToString(CultureInfo.InvariantCulture));
        await _settingManager.SetGlobalAsync(
            ShortLinkSettings.GeneratedCodeLength,
            input.GeneratedCodeLength.ToString(CultureInfo.InvariantCulture));

        return await GetAsync();
    }

    private async Task<int> GetValueAsync(string name, int defaultValue)
    {
        var value = await _settingProvider.GetOrNullAsync(name);
        if (value is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new AbpException($"Setting '{name}' must be an integer.");
        }

        return parsed;
    }

    private static void ValidateRange(int value, int min, int max, string settingName)
    {
        if (value < min || value > max)
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidSettingValue)
                .WithData("Setting", settingName);
        }
    }
}
