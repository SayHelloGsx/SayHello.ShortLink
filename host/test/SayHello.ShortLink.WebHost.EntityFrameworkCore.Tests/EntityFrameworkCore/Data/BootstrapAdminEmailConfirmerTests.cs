using System.Threading.Tasks;
using SayHello.ShortLink.WebHost.Data;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Data;

public class BootstrapAdminEmailConfirmerTests : WebHostEntityFrameworkCoreTestBase
{
    private readonly BootstrapAdminEmailConfirmer _confirmer;
    private readonly IdentityUserManager _userManager;

    public BootstrapAdminEmailConfirmerTests()
    {
        _confirmer = GetRequiredService<BootstrapAdminEmailConfirmer>();
        _userManager = GetRequiredService<IdentityUserManager>();
    }

    [Fact]
    public async Task EnsureConfirmedAsync_Should_Confirm_The_Bootstrap_Administrator()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var admin = await _userManager.FindByEmailAsync(
                IdentityDataSeedContributor.AdminEmailDefaultValue);
            admin.ShouldNotBeNull();
            admin.SetEmailConfirmed(false);
            await _userManager.UpdateAsync(admin);
        });

        await WithUnitOfWorkAsync(() =>
            _confirmer.EnsureConfirmedAsync(IdentityDataSeedContributor.AdminEmailDefaultValue));

        await WithUnitOfWorkAsync(async () =>
        {
            var admin = await _userManager.FindByEmailAsync(
                IdentityDataSeedContributor.AdminEmailDefaultValue);
            admin.ShouldNotBeNull();
            admin.EmailConfirmed.ShouldBeTrue();
        });
    }
}
