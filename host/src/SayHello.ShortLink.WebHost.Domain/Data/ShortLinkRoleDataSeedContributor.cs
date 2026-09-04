using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using SayHello.ShortLink.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Identity.Settings;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Roles;
using Volo.Abp.SettingManagement;

namespace SayHello.ShortLink.WebHost.Data;

public class ShortLinkRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string UserRoleName = "user";

    private readonly IdentityRoleManager _roleManager;
    private readonly IPermissionDataSeeder _permissionDataSeeder;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ISettingManager _settingManager;

    public ShortLinkRoleDataSeedContributor(
        IdentityRoleManager roleManager,
        IPermissionDataSeeder permissionDataSeeder,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        ISettingManager settingManager)
    {
        _roleManager = roleManager;
        _permissionDataSeeder = permissionDataSeeder;
        _currentTenant = currentTenant;
        _guidGenerator = guidGenerator;
        _settingManager = settingManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        using (_currentTenant.Change(context.TenantId))
        {
            var role = await _roleManager.FindByNameAsync(UserRoleName);
            if (role is null)
            {
                role = new IdentityRole(
                    _guidGenerator.Create(),
                    UserRoleName,
                    context.TenantId)
                {
                    IsDefault = true,
                    IsPublic = true
                };
                (await _roleManager.CreateAsync(role)).CheckErrors();
            }
            else if (!role.IsDefault || !role.IsPublic)
            {
                role.IsDefault = true;
                role.IsPublic = true;
                (await _roleManager.UpdateAsync(role)).CheckErrors();
            }

            await _permissionDataSeeder.SeedAsync(
                RolePermissionValueProvider.ProviderName,
                role.Name,
                [
                    ShortLinkPublicPermissions.Default,
                    ShortLinkPublicPermissions.Create,
                    ShortLinkPublicPermissions.Update,
                    ShortLinkPublicPermissions.Delete,
                    ShortLinkPublicPermissions.ViewStatistics
                ],
                context.TenantId);

            if (context.TenantId is null)
            {
                await _settingManager.SetGlobalAsync(
                    IdentitySettingNames.SignIn.RequireConfirmedEmail,
                    bool.TrueString);
            }
        }
    }
}
