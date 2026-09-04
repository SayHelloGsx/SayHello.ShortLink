using System;
using System.Threading;
using System.Threading.Tasks;
using SayHello.ShortLink.Public.ShortLinks;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace SayHello.ShortLink.WebHost.ShortLinks;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IShortLinkUserEligibilityChecker))]
public class ConfirmedEmailShortLinkUserEligibilityChecker :
    IShortLinkUserEligibilityChecker,
    ITransientDependency
{
    private readonly IdentityUserManager _userManager;

    public ConfirmedEmailShortLinkUserEligibilityChecker(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    public async Task EnsureEligibleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString("D"));
        if (user is null || !user.EmailConfirmed)
        {
            throw new BusinessException(ShortLinkErrorCodes.EmailNotConfirmed);
        }
    }
}
