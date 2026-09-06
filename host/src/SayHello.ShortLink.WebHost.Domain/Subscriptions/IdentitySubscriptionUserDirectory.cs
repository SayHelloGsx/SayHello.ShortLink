using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SayHello.Subscription;
using SayHello.Subscription.Users;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace SayHello.ShortLink.WebHost.Subscriptions;

[ExposeServices(typeof(ISubscriptionUserDirectory))]
public class IdentitySubscriptionUserDirectory : ISubscriptionUserDirectory, ITransientDependency
{
    private readonly IIdentityUserRepository _users;
    private readonly ICurrentTenant _currentTenant;

    public IdentitySubscriptionUserDirectory(IIdentityUserRepository users, ICurrentTenant currentTenant)
    {
        _users = users;
        _currentTenant = currentTenant;
    }

    public async Task<SubscriptionUserData?> FindAsync(
        Guid? tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_currentTenant.Id, tenantId);
        SubscriptionGuard.Id(userId, nameof(userId));
        var user = await _users.FindAsync(userId, includeDetails: false, cancellationToken);
        return user is null ? null : ToData(tenantId, user);
    }

    public async Task<SubscriptionPage<SubscriptionUserData>> SearchAsync(
        Guid? tenantId, string? filter, int skipCount, int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_currentTenant.Id, tenantId);
        SubscriptionGuard.Paging(skipCount, maxResultCount);
        if (filter?.Length > SubscriptionConsts.MaxNameLength)
        {
            throw new BusinessException(SubscriptionErrorCodes.InvalidPaging);
        }

        filter = filter?.Trim();
        var count = await _users.GetCountAsync(filter: filter, cancellationToken: cancellationToken);
        var users = await _users.GetListAsync(
            sorting: "userName asc, id asc",
            maxResultCount: maxResultCount,
            skipCount: skipCount,
            filter: filter,
            includeDetails: false,
            cancellationToken: cancellationToken);
        return new SubscriptionPage<SubscriptionUserData>(count, users.Select(user => ToData(tenantId, user)));
    }

    public async Task<IReadOnlyList<SubscriptionUserData>> GetByIdsAsync(
        Guid? tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        SubscriptionGuard.SameTenant(_currentTenant.Id, tenantId);
        ArgumentNullException.ThrowIfNull(userIds);
        foreach (var id in userIds)
        {
            SubscriptionGuard.Id(id, nameof(userIds));
        }

        if (userIds.Count == 0)
        {
            return [];
        }

        SubscriptionGuard.Paging(0, userIds.Count);
        var users = await _users.GetListByIdsAsync(
            userIds.Distinct(), includeDetails: false, cancellationToken);
        return users.Select(user => ToData(tenantId, user)).ToList();
    }

    private static SubscriptionUserData ToData(Guid? tenantId, IdentityUser user)
    {
        SubscriptionGuard.SameTenant(tenantId, user.TenantId);
        return new SubscriptionUserData(
            user.Id, user.TenantId, user.UserName, user.Name, user.Surname, user.Email, user.IsActive);
    }
}
