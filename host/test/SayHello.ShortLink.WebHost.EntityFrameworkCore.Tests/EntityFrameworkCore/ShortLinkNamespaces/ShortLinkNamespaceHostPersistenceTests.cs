using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
using SayHello.ShortLink.ShortLinks;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.ShortLinkNamespaces;

[Collection(WebHostTestConsts.CollectionDefinitionName)]
public class ShortLinkNamespaceHostPersistenceTests : WebHostEntityFrameworkCoreTestBase
{
    [Fact]
    public async Task Replacement_Host_Should_Resolve_Custom_Namespace_Repositories_And_The_EnsureCreated_Seed()
    {
        await InUnitAsync(null, async services =>
        {
            var namespaces = services.GetRequiredService<IShortLinkNamespaceRepository>();
            ((namespaces as IProxyTargetAccessor)?.DynProxyGetTarget() ?? namespaces)
                .ShouldBeOfType<EfCoreShortLinkNamespaceRepository>();
            var reservations = services.GetRequiredService<IShortLinkNamespaceReservationRepository>();
            ((reservations as IProxyTargetAccessor)?.DynProxyGetTarget() ?? reservations)
                .ShouldBeOfType<EfCoreShortLinkNamespaceReservationRepository>();
            var context = await services.GetRequiredService<IDbContextProvider<IShortLinkDbContext>>().GetDbContextAsync();
            context.ShouldBeOfType<WebHostDbContext>();
            (await reservations.IsReservedAsync("s")).ShouldBeTrue();
            (await reservations.IsReservedAsync("S")).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Replacement_Repositories_Should_Keep_Global_Occupation_But_Tenant_Scoped_Counts()
    {
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var namespaceId = Guid.NewGuid();
        var domain = new ShortLinkDomain(Guid.NewGuid(), tenant, "https://host-namespace.example.test");
        var linkId = Guid.NewGuid();
        await InUnitAsync(tenant, async services =>
        {
            await services.GetRequiredService<IShortLinkDomainRepository>().InsertAsync(domain, autoSave: true);
            var context = await services.GetRequiredService<IDbContextProvider<IShortLinkDbContext>>().GetDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ShortLinkNamespaces"
                    ("Id", "TenantId", "OwnerUserId", "DomainId", "Origin", "Name",
                     "ExtraProperties", "ConcurrencyStamp", "CreationTime")
                VALUES ({namespaceId}, {tenant}, {owner}, {domain.Id}, {domain.Origin}, 'Team',
                    {"{}"}, 'namespace-host-test', {DateTime.UtcNow});
                """);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ShortLinkLinks"
                    ("Id", "TenantId", "OwnerUserId", "DomainId", "Origin", "Namespace", "NamespaceId",
                     "Code", "TargetUrl", "Status", "IsCustomCode", "TotalVisitCount", "IsDeleted",
                     "ExtraProperties", "ConcurrencyStamp", "CreationTime")
                VALUES ({linkId}, {tenant}, {owner}, {domain.Id}, {domain.Origin}, 'Team', {namespaceId},
                    'TenantTuple', 'https://example.com/', {(int)ShortLinkStatus.Disabled}, {false}, 0, {true},
                    {"{}"}, 'link-host-test', {DateTime.UtcNow});
                """);
        });

        await InUnitAsync(null, async services =>
        {
            var namespaces = services.GetRequiredService<IShortLinkNamespaceRepository>();
            var links = services.GetRequiredService<IShortLinkRepository>();
            (await namespaces.NameExistsAsync(domain.Origin, "Team")).ShouldBeTrue();
            (await namespaces.NameExistsAsync("Team")).ShouldBeTrue();
            (await namespaces.GetCountByOwnerAsync(owner)).ShouldBe(0);
            (await namespaces.GetListByOwnerAsync(owner)).ShouldBeEmpty();
            (await namespaces.HasLinkReferencesAsync(namespaceId)).ShouldBeTrue();
            (await namespaces.HasDomainReferencesAsync(domain.Id)).ShouldBeTrue();
            (await links.CodeExistsAsync(domain.Origin, "Team", "TenantTuple")).ShouldBeTrue();
            (await links.CodeExistsAsync(domain.Origin, "TenantTuple")).ShouldBeFalse();
            (await links.FindByCodeAsync(domain.Origin, "Team", "TenantTuple")).ShouldBeNull();
            (await links.FindByCodeAsync(domain.Origin, "Team", "TenantTuple", includeDeleted: true))!
                .Id.ShouldBe(linkId);
            (await links.GetRootLinkCountByOwnerAsync(owner)).ShouldBe(0);
        });
        await InUnitAsync(tenant, async services =>
        {
            var namespaces = services.GetRequiredService<IShortLinkNamespaceRepository>();
            (await namespaces.GetCountByOwnerAsync(owner)).ShouldBe(1);
            (await namespaces.GetListByOwnerAsync(owner, domain.Origin))
                .ShouldHaveSingleItem().Id.ShouldBe(namespaceId);
            (await services.GetRequiredService<IShortLinkRepository>().GetCountByOwnerAsync(owner)).ShouldBe(0);
        });
    }

    private async Task InUnitAsync(Guid? tenantId, Func<IServiceProvider, Task> action)
    {
        using var scope = ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var unit = services.GetRequiredService<IUnitOfWorkManager>()
            .Begin(requiresNew: true, isTransactional: true);
        await action(services);
        await unit.CompleteAsync();
    }
}
