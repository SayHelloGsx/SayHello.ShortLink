using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace SayHello.Subscription.Users;

public class SubscriptionUserLookupService : ISubscriptionUserLookupService, ITransientDependency
{
    private readonly IExternalUserLookupServiceProvider _externalUsers;
    private readonly ICurrentTenant _currentTenant;

    public SubscriptionUserLookupService(
        IExternalUserLookupServiceProvider externalUsers,
        ICurrentTenant currentTenant)
    {
        _externalUsers = externalUsers;
        _currentTenant = currentTenant;
    }

    public async Task<SubscriptionUser> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.Id(id, nameof(id));
        var user = await _externalUsers.FindByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null!;
        }

        EnsureCurrentTenant(user);
        if (user.Id != id)
        {
            throw new BusinessException(SubscriptionErrorCodes.UserNotFound);
        }

        return new SubscriptionUser(user);
    }

    public async Task<SubscriptionUser> FindByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        var user = await _externalUsers.FindByUserNameAsync(userName, cancellationToken);
        if (user is null)
        {
            return null!;
        }

        EnsureCurrentTenant(user);
        return new SubscriptionUser(user);
    }

    public async Task<List<IUserData>> SearchAsync(
        string? sorting = null,
        string? filter = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        var users = await _externalUsers.SearchAsync(
            sorting,
            filter,
            maxResultCount,
            skipCount,
            cancellationToken);
        if (users is null)
        {
            throw new AbpException("The external user lookup provider returned a null search result.");
        }

        foreach (var user in users)
        {
            if (user is null)
            {
                throw new AbpException("The external user lookup provider returned a null user.");
            }

            EnsureCurrentTenant(user);
        }

        return users;
    }

    public async Task<long> GetCountAsync(
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var count = await _externalUsers.GetCountAsync(filter, cancellationToken);
        if (count < 0)
        {
            throw new AbpException("The external user lookup provider returned a negative user count.");
        }

        return count;
    }

    private void EnsureCurrentTenant(IUserData user) =>
        SubscriptionGuard.SameTenant(_currentTenant.Id, user.TenantId);
}
