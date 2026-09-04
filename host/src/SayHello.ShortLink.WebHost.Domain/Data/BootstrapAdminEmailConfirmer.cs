using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace SayHello.ShortLink.WebHost.Data;

public class BootstrapAdminEmailConfirmer : ITransientDependency
{
    private readonly IdentityUserManager _identityUserManager;

    public BootstrapAdminEmailConfirmer(IdentityUserManager identityUserManager)
    {
        _identityUserManager = identityUserManager;
    }

    public async Task EnsureConfirmedAsync(string emailAddress)
    {
        var adminUser = await _identityUserManager.FindByEmailAsync(emailAddress);
        if (adminUser is null || adminUser.EmailConfirmed)
        {
            return;
        }

        adminUser.SetEmailConfirmed(true);
        (await _identityUserManager.UpdateAsync(adminUser)).CheckErrors();
    }
}
