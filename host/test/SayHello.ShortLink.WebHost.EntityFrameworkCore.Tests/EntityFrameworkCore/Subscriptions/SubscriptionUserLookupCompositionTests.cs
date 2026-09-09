using System;
using System.Threading.Tasks;
using SayHello.Subscription.Users;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

[Collection(WebHostTestConsts.CollectionDefinitionName)]
public class SubscriptionUserLookupCompositionTests :
    WebHostDomainTestBase<WebHostEntityFrameworkCoreTestModule>
{
    [Fact]
    public async Task Local_host_uses_ABP_identity_provider_without_a_subscription_adapter()
    {
        GetRequiredService<IExternalUserLookupServiceProvider>()
            .ShouldBeOfType<IdentityUserRepositoryExternalUserLookupServiceProvider>();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<IIdentityUserRepository>().InsertAsync(
                new IdentityUser(userId, "subscription-lookup", "subscription-lookup@example.test", tenantId),
                autoSave: true);
        });

        SubscriptionUser? user = null;
        await WithUnitOfWorkAsync(async () =>
        {
            user = await GetRequiredService<ISubscriptionUserLookupService>()
                .FindByIdAsync(userId);
        });

        user.ShouldNotBeNull();
        user.Id.ShouldBe(userId);
        user.TenantId.ShouldBe(tenantId);
        user.UserName.ShouldBe("subscription-lookup");
    }
}
