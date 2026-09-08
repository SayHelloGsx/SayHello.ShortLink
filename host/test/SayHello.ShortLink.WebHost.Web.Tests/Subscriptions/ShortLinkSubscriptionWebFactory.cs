using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SayHello.ShortLink.Permissions;
using SayHello.ShortLink.ShortLinks;
using SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;
using ShortLinkEntity = SayHello.ShortLink.ShortLinks.ShortLink;

namespace SayHello.ShortLink.WebHost.Subscriptions;

public class ShortLinkSubscriptionWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid DisabledOwner = Guid.NewGuid();
    public static readonly Guid EnabledOwner = Guid.NewGuid();
    public static readonly Guid NoPermissionOwner = Guid.NewGuid();
    public static readonly Guid NoStatisticsPermissionOwner = Guid.NewGuid();
    public static readonly Guid UnlimitedOwner = Guid.NewGuid();
    public static readonly Guid DeniedOwner = Guid.NewGuid();
    public static readonly Guid Administrator = Guid.NewGuid();
    public ShortLinkEntity DisabledLink { get; private set; } = null!;
    public ShortLinkEntity EnabledLink { get; private set; } = null!;
    public ShortLinkEntity NoStatisticsPermissionLink { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var services = scope.ServiceProvider;
        using var unit = services.GetRequiredService<IUnitOfWorkManager>()
            .Begin(requiresNew: true, isTransactional: true);
        var users = services.GetRequiredService<IdentityUserManager>();
        var permissions = services.GetRequiredService<IPermissionManager>();
        foreach (var id in new[]
                 {
                     DisabledOwner, EnabledOwner, NoPermissionOwner, NoStatisticsPermissionOwner,
                     UnlimitedOwner, DeniedOwner, Administrator
                 })
        {
            var name = "bridge-web-" + id.ToString("N");
            var user = new IdentityUser(id, name, name + "@example.test");
            user.SetEmailConfirmed(true);
            (await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
            if (id == NoPermissionOwner) continue;
            foreach (var permission in new[]
                     {
                         ShortLinkPublicPermissions.Default, ShortLinkPublicPermissions.Create,
                         ShortLinkPublicPermissions.Update, ShortLinkPublicPermissions.Delete,
                         ShortLinkPublicPermissions.ViewStatistics
                     })
            {
                if (id == NoStatisticsPermissionOwner && permission != ShortLinkPublicPermissions.Default) continue;
                await permissions.SetForUserAsync(id, permission, true);
            }
        }
        await permissions.SetForUserAsync(Administrator, ShortLinkAdminPermissions.ManageAllLinks, true);
        var free = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 20, false, setDefault: false);
        var pro = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, 100, true, setDefault: false);
        var unlimited = await ShortLinkSubscriptionTestData.CreatePlanAsync(services, null, false, unlimited: true, setDefault: false);
        await ShortLinkSubscriptionTestData.AssignAsync(services, DisabledOwner, free.Id);
        await ShortLinkSubscriptionTestData.AssignAsync(services, EnabledOwner, pro.Id);
        await ShortLinkSubscriptionTestData.AssignAsync(services, NoPermissionOwner, pro.Id);
        await ShortLinkSubscriptionTestData.AssignAsync(services, NoStatisticsPermissionOwner, pro.Id);
        await ShortLinkSubscriptionTestData.AssignAsync(services, UnlimitedOwner, unlimited.Id);
        DisabledLink = await CreateVisitedLinkAsync(services, DisabledOwner);
        EnabledLink = await CreateVisitedLinkAsync(services, EnabledOwner);
        NoStatisticsPermissionLink = await CreateVisitedLinkAsync(services, NoStatisticsPermissionOwner);
        await unit.CompleteAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    private static async Task<ShortLinkEntity> CreateVisitedLinkAsync(IServiceProvider services, Guid owner)
    {
        var input = ShortLinkSubscriptionTestData.NewLink();
        var entity = await services.GetRequiredService<ShortLinkManager>().CreateAndSaveAsync(
            Guid.NewGuid(), null, owner, input.TargetUrl, input.CustomCode, input.Title, null);
        for (var i = 0; i < 7; i++) entity.IncreaseVisitCount();
        await services.GetRequiredService<IShortLinkRepository>().UpdateAsync(entity, autoSave: true);
        return entity;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            ShortLinkSubscriptionTestData.ConfigureExternalAdapters(services);
            services.Replace(ServiceDescriptor.Transient<IAuthorizationService, AbpAuthorizationService>());
            services.Replace(ServiceDescriptor.Transient<IAbpAuthorizationService, AbpAuthorizationService>());
            services.Replace(ServiceDescriptor.Transient<IMethodInvocationAuthorizationService, MethodInvocationAuthorizationService>());
            services.Replace(ServiceDescriptor.Transient<IPermissionChecker, PermissionChecker>());
            services.Replace(ServiceDescriptor.Singleton<ICurrentPrincipalAccessor, SubscriptionHttpPrincipalAccessor>());
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SubscriptionTestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = SubscriptionTestAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = SubscriptionTestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, SubscriptionTestAuthenticationHandler>(
                SubscriptionTestAuthenticationHandler.SchemeName, _ => { });
        });
    }
}
