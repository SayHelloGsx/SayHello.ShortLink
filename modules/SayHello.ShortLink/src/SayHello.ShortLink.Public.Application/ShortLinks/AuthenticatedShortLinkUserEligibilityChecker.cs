using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace SayHello.ShortLink.Public.ShortLinks;

public class AuthenticatedShortLinkUserEligibilityChecker :
    IShortLinkUserEligibilityChecker,
    ITransientDependency
{
    private readonly ICurrentUser _currentUser;

    public AuthenticatedShortLinkUserEligibilityChecker(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task EnsureEligibleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.Id != userId)
        {
            throw new AbpAuthorizationException();
        }

        return Task.CompletedTask;
    }
}
