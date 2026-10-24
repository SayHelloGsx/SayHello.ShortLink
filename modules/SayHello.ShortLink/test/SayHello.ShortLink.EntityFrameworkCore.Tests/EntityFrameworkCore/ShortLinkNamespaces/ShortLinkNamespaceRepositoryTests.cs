using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Xunit;
using Link = SayHello.ShortLink.ShortLinks.ShortLink;

namespace SayHello.ShortLink.ShortLinkNamespaces;

public class ShortLinkNamespaceRepositoryTests : ShortLinkEntityFrameworkCoreTestBase
{
    private readonly IShortLinkRepository _links;
    private readonly IShortLinkNamespaceRepository _namespaces;
    private readonly IShortLinkNamespaceReservationRepository _reservations;
    private readonly IShortLinkDomainRepository _domains;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guids;
    private readonly IDbContextProvider<IShortLinkDbContext> _contexts;

    public ShortLinkNamespaceRepositoryTests()
    {
        _links = GetRequiredService<IShortLinkRepository>();
        _namespaces = GetRequiredService<IShortLinkNamespaceRepository>();
        _reservations = GetRequiredService<IShortLinkNamespaceReservationRepository>();
        _domains = GetRequiredService<IShortLinkDomainRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guids = GetRequiredService<IGuidGenerator>();
        _contexts = GetRequiredService<IDbContextProvider<IShortLinkDbContext>>();
    }

    [Fact]
    public async Task Lookup_Should_Use_The_Exact_Case_Sensitive_Tuple_And_Keep_Old_Overloads_Root_Only()
    {
        var owner = _guids.Create();
        var first = await CreateDomainAsync("https://tuple-one.example.test");
        var second = await CreateDomainAsync("https://tuple-two.example.test");
        var root = CreateLink(first, owner, "SameCode");
        var defaults = CreateLink(first, owner, "SameCode", "s");
        var uppercase = CreateLink(first, owner, "SameCode", "Team");
        var lowercase = CreateLink(first, owner, "SameCode", "team");
        var otherOrigin = CreateLink(second, owner, "SameCode", "Team");
        var legacy = CreateLegacy(owner, "SameCode");
        var namespaceOnly = CreateLink(first, owner, "NamespaceOnly", "s");
        await WithUnitOfWorkAsync(async () =>
        {
            foreach (var link in new[] { root, defaults, uppercase, lowercase, otherOrigin, legacy, namespaceOnly })
                await _links.InsertAsync(link, autoSave: true);
        });

        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(first.Origin, "SameCode")))!.Id.ShouldBe(root.Id);
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(first.Origin, "", "SameCode")))!.Id.ShouldBe(root.Id);
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(first.Origin, "s", "SameCode")))!.Id.ShouldBe(defaults.Id);
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(first.Origin, "Team", "SameCode")))!.Id.ShouldBe(uppercase.Id);
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(first.Origin, "team", "SameCode")))!.Id.ShouldBe(lowercase.Id);
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(second.Origin, "Team", "SameCode")))!.Id.ShouldBe(otherOrigin.Id);
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(first.Origin, "TEAM", "SameCode"))).ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(first.Origin, "Team", "samecode"))).ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(first.Origin, "NamespaceOnly"))).ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _links.CodeExistsAsync(first.Origin, "NamespaceOnly"))).ShouldBeFalse();
        (await WithUnitOfWorkAsync(() => _links.CodeExistsAsync(first.Origin, "s", "NamespaceOnly"))).ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync("SameCode")))!.Id.ShouldBe(legacy.Id);
        (await WithUnitOfWorkAsync(() => _links.FindLegacyByCodeAsync("SameCode")))!.Id.ShouldBe(legacy.Id);
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync("NamespaceOnly"))).ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _links.CodeExistsAsync("NamespaceOnly"))).ShouldBeFalse();
    }

    [Fact]
    public async Task Deleted_Tuples_And_Legacy_Codes_Should_Remain_Occupied_Across_All_Tenants()
    {
        var tenant = _guids.Create();
        var otherTenant = _guids.Create();
        var owner = _guids.Create();
        ShortLinkDomain domain;
        Link root;
        Link namespaced;
        Link legacy;
        using (_currentTenant.Change(tenant))
        {
            domain = await CreateDomainAsync("https://occupied.example.test");
            root = CreateLink(domain, owner, "DeletedRoot");
            namespaced = CreateLink(domain, owner, "DeletedTuple", "Team");
            legacy = CreateLegacy(owner, "DeletedLegacy", tenant);
            await WithUnitOfWorkAsync(async () =>
            {
                foreach (var link in new[] { root, namespaced, legacy })
                {
                    await _links.InsertAsync(link, autoSave: true);
                    await _links.DeleteAsync(link, autoSave: true);
                }
            });
        }

        foreach (var scope in new Guid?[] { null, tenant, otherTenant })
        {
            using (_currentTenant.Change(scope))
            {
                (await WithUnitOfWorkAsync(() => _links.CodeExistsAsync(domain.Origin, "DeletedRoot"))).ShouldBeTrue();
                (await WithUnitOfWorkAsync(() => _links.CodeExistsAsync(domain.Origin, "Team", "DeletedTuple"))).ShouldBeTrue();
                (await WithUnitOfWorkAsync(() => _links.CodeExistsAsync("DeletedLegacy"))).ShouldBeTrue();
                (await WithUnitOfWorkAsync(() => _links.CodeExistsAsync(domain.Origin, "DeletedTuple"))).ShouldBeFalse();
                (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(domain.Origin, "Team", "DeletedTuple"))).ShouldBeNull();
                (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(
                    domain.Origin, "Team", "DeletedTuple", includeDeleted: true)))!.IsDeleted.ShouldBeTrue();
                (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(domain.Origin, "DeletedRoot"))).ShouldBeNull();
                (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(
                    domain.Origin, "DeletedRoot", includeDeleted: true)))!.Id.ShouldBe(root.Id);
                (await WithUnitOfWorkAsync(() => _links.FindLegacyByCodeAsync("DeletedLegacy"))).ShouldBeNull();
                (await WithUnitOfWorkAsync(() => _links.FindLegacyByCodeAsync(
                    "DeletedLegacy", includeDeleted: true)))!.Id.ShouldBe(legacy.Id);
            }
        }

        var hostDomain = await CreateDomainAsync(domain.Origin);
        await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
            _links.InsertAsync(CreateLink(hostDomain, _guids.Create(), "DeletedTuple", "Team"), autoSave: true)));
        await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
            _links.InsertAsync(CreateLink(hostDomain, _guids.Create(), "DeletedRoot"), autoSave: true)));
        await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
            _links.InsertAsync(CreateLegacy(_guids.Create(), "DeletedLegacy"), autoSave: true)));
    }

    [Fact]
    public async Task Namespace_Names_Should_Be_Global_Per_Origin_Case_Sensitive_And_Owner_Counts_Tenant_Scoped()
    {
        var owner = _guids.Create();
        var tenant = _guids.Create();
        var otherTenant = _guids.Create();
        var first = await CreateDomainAsync("https://namespace-one.example.test");
        var second = await CreateDomainAsync("https://namespace-two.example.test");
        var name = CreateNamespace(first, owner, "Team");
        var caseVariant = CreateNamespace(first, _guids.Create(), "team");
        var otherOrigin = CreateNamespace(second, owner, "Team");
        await WithUnitOfWorkAsync(async () =>
        {
            foreach (var item in new[] { name, caseVariant, otherOrigin })
                await _namespaces.InsertAsync(item, autoSave: true);
        });

        await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
            _namespaces.InsertAsync(CreateNamespace(first, _guids.Create(), "Team"), autoSave: true)));
        using (_currentTenant.Change(tenant))
        {
            var tenantDomain = await CreateDomainAsync(first.Origin);
            await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
                _namespaces.InsertAsync(CreateNamespace(tenantDomain, owner, "Team"), autoSave: true)));
            await WithUnitOfWorkAsync(() => _namespaces.InsertAsync(
                CreateNamespace(tenantDomain, owner, "TenantTeam"), autoSave: true));
        }
        using (_currentTenant.Change(otherTenant))
        {
            var tenantDomain = await CreateDomainAsync(second.Origin);
            await WithUnitOfWorkAsync(() => _namespaces.InsertAsync(
                CreateNamespace(tenantDomain, owner, "OtherTenantTeam"), autoSave: true));
            (await WithUnitOfWorkAsync(() => _namespaces.NameExistsAsync(first.Origin, "Team"))).ShouldBeTrue();
            (await WithUnitOfWorkAsync(() => _namespaces.NameExistsAsync("TenantTeam"))).ShouldBeTrue();
            (await WithUnitOfWorkAsync(() => _namespaces.NameExistsAsync("TEAM"))).ShouldBeFalse();
            (await WithUnitOfWorkAsync(() => _namespaces.GetCountByOwnerAsync(owner))).ShouldBe(1);
        }

        (await WithUnitOfWorkAsync(() => _namespaces.GetCountByOwnerAsync(owner))).ShouldBe(2);
        var hostList = await WithUnitOfWorkAsync(() => _namespaces.GetListByOwnerAsync(owner));
        hostList.Select(x => x.Id).ShouldBe(new[] { name.Id, otherOrigin.Id });
        (await WithUnitOfWorkAsync(() => _namespaces.GetListByOwnerAsync(owner, second.Origin)))
            .ShouldHaveSingleItem().Id.ShouldBe(otherOrigin.Id);
        using (_currentTenant.Change(tenant))
        {
            (await WithUnitOfWorkAsync(() => _namespaces.GetCountByOwnerAsync(owner))).ShouldBe(1);
            (await WithUnitOfWorkAsync(() => _namespaces.GetListByOwnerAsync(owner)))
                .ShouldHaveSingleItem().Name.ShouldBe("TenantTeam");
        }
        GetRequiredService<IDataFilter<IMultiTenant>>().IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureCreated_Should_Seed_The_Global_Case_Sensitive_Reservation()
    {
        (await WithUnitOfWorkAsync(() => _reservations.IsReservedAsync("s"))).ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => _reservations.IsReservedAsync("S"))).ShouldBeFalse();
        using (_currentTenant.Change(_guids.Create()))
        {
            (await WithUnitOfWorkAsync(() => _reservations.IsReservedAsync("s"))).ShouldBeTrue();
            await WithUnitOfWorkAsync(() => _reservations.InsertAsync(
                new ShortLinkNamespaceReservation("History"), autoSave: true));
        }
        (await WithUnitOfWorkAsync(() => _reservations.IsReservedAsync("History"))).ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => _reservations.IsReservedAsync("history"))).ShouldBeFalse();
        await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
            _reservations.InsertAsync(new ShortLinkNamespaceReservation("s"), autoSave: true)));
    }

    [Fact]
    public async Task Root_Count_Should_Include_Legacy_Disabled_And_Expired_Rows_But_Not_Deleted_Or_Namespaced_Rows()
    {
        var owner = _guids.Create();
        var domain = await CreateDomainAsync("https://root-count.example.test");
        var active = CreateLink(domain, owner, "Active");
        var disabled = CreateLink(domain, owner, "Disabled");
        disabled.Disable();
        var expired = CreateLink(domain, owner, "Expired", expiresAt: DateTime.UtcNow.AddDays(-1));
        var legacy = CreateLegacy(owner, "Legacy");
        var deleted = CreateLink(domain, owner, "Deleted");
        await WithUnitOfWorkAsync(async () =>
        {
            foreach (var link in new[]
                     {
                         active, disabled, expired, legacy, deleted,
                         CreateLink(domain, owner, "DefaultNamespace", "s"),
                         CreateLink(domain, owner, "UserNamespace", "Team"),
                         CreateLink(domain, _guids.Create(), "OtherOwner")
                     })
                await _links.InsertAsync(link, autoSave: true);
            await _links.DeleteAsync(deleted, autoSave: true);
        });
        var tenant = _guids.Create();
        using (_currentTenant.Change(tenant))
        {
            var tenantDomain = await CreateDomainAsync(domain.Origin);
            await WithUnitOfWorkAsync(async () =>
            {
                await _links.InsertAsync(CreateLink(tenantDomain, owner, "TenantRoot"), autoSave: true);
                await _links.InsertAsync(CreateLink(tenantDomain, owner, "TenantNamespaced", "s"), autoSave: true);
            });
            (await WithUnitOfWorkAsync(() => _links.GetRootLinkCountByOwnerAsync(owner))).ShouldBe(1);
            (await WithUnitOfWorkAsync(() => _links.GetCountByOwnerAsync(owner))).ShouldBe(2);
        }
        (await WithUnitOfWorkAsync(() => _links.GetRootLinkCountByOwnerAsync(owner))).ShouldBe(4);
        (await WithUnitOfWorkAsync(() => _links.GetCountByOwnerAsync(owner))).ShouldBe(6);
    }

    [Fact]
    public async Task References_Should_Include_Deleted_Links_In_Other_Tenants_And_Restrict_Physical_Deletion()
    {
        var namespaceTenant = _guids.Create();
        var linkTenant = _guids.Create();
        var owner = _guids.Create();
        ShortLinkDomain domain;
        ShortLinkNamespace item;
        using (_currentTenant.Change(namespaceTenant))
        {
            domain = await CreateDomainAsync("https://reference.example.test");
            item = CreateNamespace(domain, owner, "Team");
            await WithUnitOfWorkAsync(() => _namespaces.InsertAsync(item, autoSave: true));
        }
        using (_currentTenant.Change(linkTenant))
        {
            var linkDomain = await CreateDomainAsync(domain.Origin);
            var link = CreateLink(linkDomain, owner, "Referenced", item.Name, item.Id);
            await WithUnitOfWorkAsync(async () =>
            {
                await _links.InsertAsync(link, autoSave: true);
                await _links.DeleteAsync(link, autoSave: true);
            });
        }
        (await WithUnitOfWorkAsync(() => _namespaces.HasLinkReferencesAsync(item.Id))).ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => _namespaces.HasDomainReferencesAsync(domain.Id))).ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => _namespaces.HasLinkReferencesAsync(_guids.Create()))).ShouldBeFalse();
        (await WithUnitOfWorkAsync(() => _namespaces.HasDomainReferencesAsync(_guids.Create()))).ShouldBeFalse();
        using (_currentTenant.Change(namespaceTenant))
        {
            await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(async () =>
                await _namespaces.DeleteAsync(await _namespaces.GetAsync(item.Id), autoSave: true)));
            var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
                await _domains.DeleteAsync(await _domains.GetAsync(domain.Id), autoSave: true)));
            exception.Code.ShouldBe(ShortLinkErrorCodes.DomainInUse);
            var foreignKey = await Should.ThrowAsync<SqliteException>(() => WithUnitOfWorkAsync(async () =>
                await (await _contexts.GetDbContextAsync()).Database.ExecuteSqlInterpolatedAsync(
                    $"""DELETE FROM "ShortLinkDomains" WHERE "Id" = {domain.Id};""")));
            foreignKey.SqliteExtendedErrorCode.ShouldBe(1811);
            foreignKey.Message.ShouldContain("FOREIGN KEY constraint failed");
        }
        GetRequiredService<IDataFilter<ISoftDelete>>().IsEnabled.ShouldBeTrue();
        GetRequiredService<IDataFilter<IMultiTenant>>().IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Referenced_Domain_Ids_Should_Batch_Single_Lookup_Semantics_Across_Tenants()
    {
        var owner = _guids.Create();
        var hostDomain = await CreateDomainAsync("https://batch-namespace-host.example.test");
        var unreferencedDomain = await CreateDomainAsync("https://batch-namespace-empty.example.test");
        await WithUnitOfWorkAsync(async () =>
        {
            await _namespaces.InsertAsync(CreateNamespace(hostDomain, owner, "First"), autoSave: true);
            await _namespaces.InsertAsync(CreateNamespace(hostDomain, owner, "Second"), autoSave: true);
        });
        ShortLinkDomain tenantDomain;
        using (_currentTenant.Change(_guids.Create()))
        {
            tenantDomain = await CreateDomainAsync("https://batch-namespace-tenant.example.test");
            await WithUnitOfWorkAsync(() =>
                _namespaces.InsertAsync(CreateNamespace(tenantDomain, owner, "Tenant"), autoSave: true));
        }

        var referenced = await WithUnitOfWorkAsync(() => _namespaces.GetReferencedDomainIdsAsync(
            [hostDomain.Id, unreferencedDomain.Id, tenantDomain.Id, hostDomain.Id]));

        referenced.ShouldBe([hostDomain.Id, tenantDomain.Id], ignoreOrder: true);
        foreach (var domainId in new[] { hostDomain.Id, unreferencedDomain.Id, tenantDomain.Id })
        {
            (await WithUnitOfWorkAsync(() => _namespaces.HasDomainReferencesAsync(domainId)))
                .ShouldBe(referenced.Contains(domainId));
        }
        (await WithUnitOfWorkAsync(() => _namespaces.GetReferencedDomainIdsAsync([]))).ShouldBeEmpty();
        GetRequiredService<IDataFilter<IMultiTenant>>().IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Legacy_Rows_Should_Reject_Namespaces_Or_Namespace_References_And_Namespace_Should_Be_Required()
    {
        var owner = _guids.Create();
        var domain = await CreateDomainAsync("https://legacy-constraint.example.test");
        var item = CreateNamespace(domain, owner, "Team");
        var legacy = CreateLegacy(owner, "LegacyConstraint");
        await WithUnitOfWorkAsync(async () =>
        {
            await _namespaces.InsertAsync(item, autoSave: true);
            await _links.InsertAsync(legacy, autoSave: true);
        });

        var nameError = await Should.ThrowAsync<SqliteException>(() => WithUnitOfWorkAsync(async () =>
            await (await _contexts.GetDbContextAsync()).Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "ShortLinkLinks" SET "Namespace" = 's' WHERE "Id" = {legacy.Id};""")));
        nameError.SqliteExtendedErrorCode.ShouldBe(275);
        var referenceError = await Should.ThrowAsync<SqliteException>(() => WithUnitOfWorkAsync(async () =>
            await (await _contexts.GetDbContextAsync()).Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "ShortLinkLinks" SET "NamespaceId" = {item.Id} WHERE "Id" = {legacy.Id};""")));
        referenceError.SqliteExtendedErrorCode.ShouldBe(275);
        var nullError = await Should.ThrowAsync<SqliteException>(() => WithUnitOfWorkAsync(async () =>
            await (await _contexts.GetDbContextAsync()).Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "ShortLinkLinks" SET "Namespace" = NULL WHERE "Id" = {legacy.Id};""")));
        nullError.SqliteExtendedErrorCode.ShouldBe(1299);
    }

    [Fact]
    public async Task Domain_Backfill_Should_Preserve_Root_Identity_And_Not_Collide_With_Namespaced_Codes()
    {
        var owner = _guids.Create();
        var domain = await CreateDomainAsync("https://backfill.example.test");
        var legacy = CreateLegacy(owner, "Backfill");
        var deleted = CreateLegacy(owner, "DeletedBackfill");
        var namespaced = CreateLink(domain, owner, legacy.Code, "s");
        await WithUnitOfWorkAsync(async () =>
        {
            await _links.InsertAsync(legacy, autoSave: true);
            await _links.InsertAsync(deleted, autoSave: true);
            await _links.DeleteAsync(deleted, autoSave: true);
            await _links.InsertAsync(namespaced, autoSave: true);
        });
        var tenant = _guids.Create();
        var tenantLegacy = CreateLegacy(owner, "TenantBackfill", tenant);
        using (_currentTenant.Change(tenant))
            await WithUnitOfWorkAsync(() => _links.InsertAsync(tenantLegacy, autoSave: true));

        (await WithUnitOfWorkAsync(() => _links.BackfillDomainAsync(domain.Id, domain.Origin))).ShouldBe(2);
        (await WithUnitOfWorkAsync(() => _links.BackfillDomainAsync(domain.Id, domain.Origin))).ShouldBe(0);
        var reloaded = await WithUnitOfWorkAsync(() => _links.GetAsync(legacy.Id));
        reloaded.Origin.ShouldBe(domain.Origin);
        reloaded.DomainId.ShouldBe(domain.Id);
        reloaded.Namespace.ShouldBeEmpty();
        reloaded.NamespaceId.ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(
            domain.Origin, "DeletedBackfill", includeDeleted: true)))!.IsDeleted.ShouldBeTrue();
        (await WithUnitOfWorkAsync(() => _links.FindByCodeAsync(domain.Origin, "s", legacy.Code)))!.Id.ShouldBe(namespaced.Id);
        using (_currentTenant.Change(tenant))
            (await WithUnitOfWorkAsync(() => _links.GetAsync(tenantLegacy.Id))).Origin.ShouldBeNull();
    }

    [Fact]
    public async Task Domain_Backfill_Should_Reject_An_Exact_Root_Collision_In_Another_Tenant()
    {
        var owner = _guids.Create();
        var domain = await CreateDomainAsync("https://backfill-collision.example.test");
        var legacy = CreateLegacy(owner, "Collision");
        await WithUnitOfWorkAsync(() => _links.InsertAsync(legacy, autoSave: true));
        using (_currentTenant.Change(_guids.Create()))
        {
            var tenantDomain = await CreateDomainAsync(domain.Origin);
            await WithUnitOfWorkAsync(() => _links.InsertAsync(
                CreateLink(tenantDomain, owner, legacy.Code), autoSave: true));
        }
        var exception = await Should.ThrowAsync<SqliteException>(() => WithUnitOfWorkAsync(() =>
            _links.BackfillDomainAsync(domain.Id, domain.Origin)));
        exception.SqliteExtendedErrorCode.ShouldBe(2067);
        (await WithUnitOfWorkAsync(() => _links.GetAsync(legacy.Id))).Origin.ShouldBeNull();
    }

    [Fact]
    public async Task Namespace_Delete_Should_Preserve_The_ABP_Concurrency_Stamp_Contract()
    {
        var domain = await CreateDomainAsync("https://namespace-concurrency.example.test");
        var item = CreateNamespace(domain, _guids.Create(), "Team");
        await WithUnitOfWorkAsync(() => _namespaces.InsertAsync(item, autoSave: true));
        var winner = await WithUnitOfWorkAsync(() => _namespaces.GetAsync(item.Id));
        var stale = await WithUnitOfWorkAsync(() => _namespaces.GetAsync(item.Id));
        var originalStamp = winner.ConcurrencyStamp;
        winner.ExtraProperties["PersistenceTest"] = true;
        await WithUnitOfWorkAsync(() => _namespaces.UpdateAsync(winner, autoSave: true));
        winner.ConcurrencyStamp.ShouldNotBe(originalStamp);
        stale.ConcurrencyStamp = originalStamp;
        await Should.ThrowAsync<AbpDbConcurrencyException>(() => WithUnitOfWorkAsync(() =>
            _namespaces.DeleteAsync(stale, autoSave: true)));
        (await WithUnitOfWorkAsync(() => _namespaces.GetAsync(item.Id))).Name.ShouldBe(item.Name);
    }

    private async Task<ShortLinkDomain> CreateDomainAsync(string origin)
    {
        var domain = new ShortLinkDomain(_guids.Create(), _currentTenant.Id, origin);
        await WithUnitOfWorkAsync(() => _domains.InsertAsync(domain, autoSave: true));
        return domain;
    }

    private ShortLinkNamespace CreateNamespace(ShortLinkDomain domain, Guid owner, string name) =>
        new(_guids.Create(), domain.TenantId, owner, domain.Id, domain.Origin, name);

    private Link CreateLink(
        ShortLinkDomain domain,
        Guid owner,
        string code,
        string pathNamespace = "",
        Guid? namespaceId = null,
        DateTime? expiresAt = null) =>
        new(_guids.Create(), domain.TenantId, owner, domain.Id, domain.Origin, code,
            "https://example.com/target", null, expiresAt, pathNamespace: pathNamespace, namespaceId: namespaceId);

    private Link CreateLegacy(Guid owner, string code, Guid? tenant = null) =>
        new(_guids.Create(), tenant, owner, code, "https://example.com/legacy", null, null);
}
