using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NpgsqlTypes;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.ShortLinkNamespaces;

public class ShortLinkNamespacePostgreSqlTests : IAsyncLifetime
{
    private const string NamespaceMigration = "20261022075604_AddShortLinkNamespaces";
    private static readonly long? Unlimited = null;
    private readonly SubscriptionPostgreSqlDatabase _database = new();
    private readonly Guid _owner = Guid.NewGuid();

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [PostgreSqlFact]
    public async Task Upgrade_And_Allowed_Downgrade_Should_Preserve_Existing_Root_Legacy_And_Deleted_Identities()
    {
        await using var context = _database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(ShortLinkNamespaceHostModelTests.PreviousMigration);
        var hostDomain = Guid.NewGuid();
        var tenantDomain = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        await InsertDomainAsync(hostDomain, null, "https://migration-root.example.test");
        await InsertDomainAsync(tenantDomain, tenant, "https://migration-tenant.example.test");
        await InsertLegacySchemaLinkAsync(Guid.NewGuid(), null, hostDomain, "https://migration-root.example.test");
        await InsertLegacySchemaLinkAsync(Guid.NewGuid(), tenant, tenantDomain, "https://migration-tenant.example.test", deleted: true);
        await InsertLegacySchemaLinkAsync(Guid.NewGuid(), tenant, null, null, deleted: true);
        var before = await IdentitySnapshotAsync();

        await migrator.MigrateAsync();
        (await IdentitySnapshotAsync()).ShouldBe(before);
        (await ScalarAsync<long>("""
            SELECT COUNT(*) FROM "ShortLinkLinks" WHERE "Namespace" = '' AND "NamespaceId" IS NULL
            """)).ShouldBe(3);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ShortLinkNamespaceReservations" WHERE "Id" = 's'"""))
            .ShouldBe(1);
        await migrator.MigrateAsync();
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ShortLinkNamespaceReservations" """)).ShouldBe(1);
        context.Database.HasPendingModelChanges().ShouldBeFalse();

        await migrator.MigrateAsync(ShortLinkNamespaceHostModelTests.PreviousMigration);
        (await IdentitySnapshotAsync()).ShouldBe(before);
        (await ScalarAsync<bool>("""SELECT to_regclass('"ShortLinkNamespaces"') IS NULL""")).ShouldBeTrue();
        (await ScalarAsync<long>("""
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_name = 'ShortLinkLinks' AND column_name IN ('Namespace', 'NamespaceId')
            """)).ShouldBe(0);
        await RejectAsync(() => InsertLegacySchemaLinkAsync(
            Guid.NewGuid(), tenant, hostDomain, "https://migration-root.example.test"), "23505", "IX_ShortLinkLinks_Origin_Code");

        await migrator.MigrateAsync();
        (await IdentitySnapshotAsync()).ShouldBe(before);
        (await ScalarAsync<long>("""
            SELECT COUNT(*) FROM "ShortLinkLinks" WHERE "Namespace" = '' AND "NamespaceId" IS NULL
            """)).ShouldBe(3);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ShortLinkNamespaceReservations" WHERE "Id" = 's'"""))
            .ShouldBe(1);
    }

    [PostgreSqlFact]
    public async Task Entitlement_Removal_Should_Revoke_Unbacked_Namespace_Quotas_And_Restore_Granted_Flags_On_Downgrade()
    {
        await using var context = _database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(NamespaceMigration);
        var host = await InsertProductAsync(null, "short-link", "host-short-link");
        var tenant = await InsertProductAsync(Guid.NewGuid(), "short-link", "tenant-short-link");
        var other = await InsertProductAsync(null, "other-product", "other-product");
        await SeedNamespaceGrantAsync(host, "backed", legacyFlag: true, quota: 5, chinese: true);
        await SeedNamespaceGrantAsync(host, "unbacked", legacyFlag: null, quota: 3);
        await SeedNamespaceGrantAsync(host, "disabled-unlimited", legacyFlag: false, quota: Unlimited);
        await SeedNamespaceGrantAsync(host, "enabled-zero", legacyFlag: true, quota: 0);
        await SeedNamespaceGrantAsync(host, "none", legacyFlag: null, quota: 0);
        await SeedNamespaceGrantAsync(host, "edited", legacyFlag: true, quota: 5);
        await SeedNamespaceGrantAsync(tenant, "tenant-unlimited", legacyFlag: true, quota: Unlimited);
        await SeedNamespaceGrantAsync(other, "other-disabled", legacyFlag: false, quota: 5);
        await SeedNamespaceGrantAsync(other, "other-quota", legacyFlag: null, quota: 5);
        // The plan disabled namespaces after assignment, so only the existing subscription snapshot is still backed.
        await ExecuteAsync(
            """
            UPDATE "SubscriptionPlanEntitlements" AS entitlement
            SET "BooleanValue" = FALSE
            FROM "SubscriptionPlans" AS plan
            WHERE entitlement."PlanId" = plan."Id" AND plan."Code" = 'edited' AND entitlement."FeatureKey" = 'namespaces'
            """);
        var seededStamps = await StampsAsync();

        await migrator.MigrateAsync();
        string[] upgraded =
        [
            "plan:backed max-links=100 max-namespaces=5",
            "plan:disabled-unlimited max-links=100 max-namespaces=0",
            "plan:edited max-links=100 max-namespaces=0",
            "plan:enabled-zero max-links=100 max-namespaces=0",
            "plan:none max-links=100 max-namespaces=0",
            "plan:other-disabled max-links=100 max-namespaces=5 namespaces=false",
            "plan:other-quota max-links=100 max-namespaces=5",
            "plan:tenant-unlimited max-links=100 max-namespaces=unlimited",
            "plan:unbacked max-links=100 max-namespaces=0",
            "subscription:backed max-links=100 max-namespaces=5",
            "subscription:disabled-unlimited max-links=100 max-namespaces=0",
            "subscription:edited max-links=100 max-namespaces=5",
            "subscription:enabled-zero max-links=100 max-namespaces=0",
            "subscription:none max-links=100 max-namespaces=0",
            "subscription:other-disabled max-links=100 max-namespaces=5 namespaces=false",
            "subscription:other-quota max-links=100 max-namespaces=5",
            "subscription:tenant-unlimited max-links=100 max-namespaces=unlimited",
            "subscription:unbacked max-links=100 max-namespaces=0"
        ];
        string[] rewritten =
        [
            "plan:backed", "plan:disabled-unlimited", "plan:edited", "plan:enabled-zero", "plan:tenant-unlimited",
            "plan:unbacked", "subscription:backed", "subscription:disabled-unlimited", "subscription:edited",
            "subscription:enabled-zero", "subscription:tenant-unlimited", "subscription:unbacked"
        ];
        (await EntitlementStateAsync()).ShouldBe(upgraded);
        var upgradedStamps = await StampsAsync();
        RotatedStamps(seededStamps, upgradedStamps).ShouldBe(rewritten);
        context.Database.HasPendingModelChanges().ShouldBeFalse();
        (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();

        await migrator.MigrateAsync(NamespaceMigration);
        (await context.Database.GetAppliedMigrationsAsync()).Last().ShouldBe(NamespaceMigration);
        // Downgrading restores namespaces=true exactly where a granted quota survived the upgrade.
        string[] restored =
        [
            "plan:backed", "plan:tenant-unlimited", "subscription:backed", "subscription:edited",
            "subscription:tenant-unlimited"
        ];
        (await EntitlementStateAsync()).ShouldBe(upgraded
            .Select(line => restored.Contains(line[..line.IndexOf(' ')]) ? line + " namespaces=true" : line)
            .ToArray());
        var downgradedStamps = await StampsAsync();
        RotatedStamps(upgradedStamps, downgradedStamps).ShouldBe(restored);
        (await ScalarAsync<string>(
            """
            SELECT string_agg(subscription."PlanCode" || '=' || entitlement."DisplayName", ';'
                ORDER BY subscription."PlanCode" COLLATE "C")
            FROM "SubscriptionUserSubscriptionEntitlements" AS entitlement
            JOIN "SubscriptionUserSubscriptions" AS subscription ON subscription."Id" = entitlement."SubscriptionId"
            WHERE subscription."ProductCode" = 'short-link' AND entitlement."FeatureKey" = 'namespaces'
            """)).ShouldBe("backed=短链接命名空间;edited=Short-link namespaces;tenant-unlimited=Short-link namespaces");
        (await ScalarAsync<long>(
            """
            SELECT
                (SELECT COUNT(*)
                 FROM "SubscriptionPlanEntitlements" AS entitlement
                 JOIN "SubscriptionPlans" AS plan ON plan."Id" = entitlement."PlanId"
                 WHERE entitlement."TenantId" IS DISTINCT FROM plan."TenantId")
                + (SELECT COUNT(*)
                   FROM "SubscriptionUserSubscriptionEntitlements" AS entitlement
                   JOIN "SubscriptionUserSubscriptions" AS subscription
                       ON subscription."Id" = entitlement."SubscriptionId"
                   WHERE entitlement."TenantId" IS DISTINCT FROM subscription."TenantId")
            """)).ShouldBe(0);

        await migrator.MigrateAsync();
        (await EntitlementStateAsync()).ShouldBe(upgraded);
        RotatedStamps(downgradedStamps, await StampsAsync()).ShouldBe(restored);
    }

    [PostgreSqlFact]
    public async Task PostgreSql_Should_Enforce_Exact_Case_Sensitive_Global_Identity_Legacy_Scope_And_Restrictive_References()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        const string origin = "https://namespace-postgres.example.test";
        const string otherOrigin = "https://other-namespace-postgres.example.test";
        var hostDomain = Guid.NewGuid();
        var tenantDomain = Guid.NewGuid();
        var otherDomain = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        await InsertDomainAsync(hostDomain, null, origin);
        await InsertDomainAsync(tenantDomain, tenant, origin);
        await InsertDomainAsync(otherDomain, tenant, otherOrigin);
        var team = Guid.NewGuid();
        var lowerTeam = Guid.NewGuid();
        var otherTeam = Guid.NewGuid();
        await InsertNamespaceAsync(team, null, hostDomain, origin, "Team", _owner);
        await InsertNamespaceAsync(lowerTeam, tenant, tenantDomain, origin, "team", Guid.NewGuid());
        await InsertNamespaceAsync(otherTeam, tenant, otherDomain, otherOrigin, "Team", Guid.NewGuid());
        await RejectAsync(() => InsertNamespaceAsync(
            Guid.NewGuid(), tenant, tenantDomain, origin, "Team", Guid.NewGuid()),
            "23505", "IX_ShortLinkNamespaces_Origin_Name");
        await ExecuteAsync("""INSERT INTO "ShortLinkNamespaceReservations" ("Id") VALUES ('S')""");
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ShortLinkNamespaceReservations" WHERE "Id" IN ('s', 'S')"""))
            .ShouldBe(2);

        await InsertLinkAsync(Guid.NewGuid(), null, hostDomain, origin, "", null);
        await InsertLinkAsync(Guid.NewGuid(), null, hostDomain, origin, "s", null);
        var deletedLink = Guid.NewGuid();
        await InsertLinkAsync(deletedLink, null, hostDomain, origin, "Team", team, deleted: true);
        await InsertLinkAsync(Guid.NewGuid(), tenant, tenantDomain, origin, "team", lowerTeam);
        await InsertLinkAsync(Guid.NewGuid(), tenant, otherDomain, otherOrigin, "Team", null);
        await InsertLinkAsync(Guid.NewGuid(), tenant, null, null, "", null, deleted: true);
        (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ShortLinkLinks" WHERE "Code" = 'SameCode'"""))
            .ShouldBe(6);
        (await ScalarAsync<long>("""
            SELECT COUNT(*) FROM "ShortLinkLinks" WHERE "Origin" = @origin AND "Namespace" = 'TEAM'
            """, Parameter("origin", NpgsqlDbType.Text, origin))).ShouldBe(0);
        await RejectAsync(() => InsertLinkAsync(
            Guid.NewGuid(), tenant, tenantDomain, origin, "Team", null),
            "23505", "IX_ShortLinkLinks_Origin_Namespace_Code");
        await RejectAsync(() => InsertLinkAsync(
            Guid.NewGuid(), tenant, tenantDomain, origin, "", null),
            "23505", "IX_ShortLinkLinks_Origin_Namespace_Code");
        await RejectAsync(() => InsertLinkAsync(
            Guid.NewGuid(), null, null, null, "", null), "23505", "IX_ShortLinkLinks_Code");
        await RejectAsync(() => InsertLinkAsync(
            Guid.NewGuid(), tenant, null, null, "s", null), "23514", "CK_ShortLinkLinks_LegacyNamespace");
        await RejectAsync(() => InsertLinkAsync(
            Guid.NewGuid(), tenant, null, null, "", team), "23514", "CK_ShortLinkLinks_LegacyNamespace");
        await RejectAsync(() => ExecuteAsync(
            """UPDATE "ShortLinkLinks" SET "Namespace" = NULL WHERE "Id" = @id""",
            Parameter("id", NpgsqlDbType.Uuid, deletedLink)), "23502");
        await RejectAsync(() => ExecuteAsync(
            """UPDATE "ShortLinkLinks" SET "Namespace" = @path WHERE "Id" = @id""",
            Parameter("path", NpgsqlDbType.Text, new string('a', 65)),
            Parameter("id", NpgsqlDbType.Uuid, deletedLink)), "22001");
        await RejectAsync(() => ExecuteAsync(
            """DELETE FROM "ShortLinkNamespaces" WHERE "Id" = @id""",
            Parameter("id", NpgsqlDbType.Uuid, team)), "23503", "FK_ShortLinkLinks_ShortLinkNamespaces_NamespaceId");
        await RejectAsync(() => ExecuteAsync(
            """DELETE FROM "ShortLinkDomains" WHERE "Id" = @id""",
            Parameter("id", NpgsqlDbType.Uuid, otherDomain)), "23503");
        await ExecuteAsync("""DELETE FROM "ShortLinkLinks" WHERE "Origin" = @origin""",
            Parameter("origin", NpgsqlDbType.Text, otherOrigin));
        await RejectAsync(() => ExecuteAsync(
            """DELETE FROM "ShortLinkDomains" WHERE "Id" = @id""",
            Parameter("id", NpgsqlDbType.Uuid, otherDomain)), "23503", "FK_ShortLinkNamespaces_ShortLinkDomains_DomainId");
    }

    [PostgreSqlTheory]
    [InlineData("s", false)]
    [InlineData("s", true)]
    [InlineData("Team", true)]
    public async Task Downgrade_Should_Reject_Any_Namespaced_Link_Including_Deleted_And_Other_Tenant_Rows(
        string pathNamespace, bool deleted)
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var tenant = Guid.NewGuid();
        var domain = Guid.NewGuid();
        const string origin = "https://namespace-downgrade.example.test";
        await InsertDomainAsync(domain, tenant, origin);
        var link = Guid.NewGuid();
        await InsertLinkAsync(link, tenant, domain, origin, pathNamespace, null, deleted);

        var exception = await RejectAsync(() => context.GetService<IMigrator>()
            .MigrateAsync(ShortLinkNamespaceHostModelTests.PreviousMigration), "P0001");
        exception.MessageText.ShouldContain("namespaced short links");
        await AssertNamespaceMigrationStillAppliedAsync(context);
        (await ScalarAsync<long>("""
            SELECT COUNT(*) FROM "ShortLinkLinks" WHERE "Id" = @id AND "Namespace" = @path AND "IsDeleted" = @deleted
            """, Parameter("id", NpgsqlDbType.Uuid, link),
            Parameter("path", NpgsqlDbType.Text, pathNamespace),
            Parameter("deleted", NpgsqlDbType.Boolean, deleted))).ShouldBe(1);
        await ExecuteAsync("""DELETE FROM "ShortLinkLinks" WHERE "Id" = @id""",
            Parameter("id", NpgsqlDbType.Uuid, link));
        await context.GetService<IMigrator>().MigrateAsync(ShortLinkNamespaceHostModelTests.PreviousMigration);
    }

    [PostgreSqlTheory]
    [InlineData("namespace")]
    [InlineData("reservation")]
    [InlineData("namespace-id")]
    public async Task Downgrade_Should_Reject_Unlinked_User_Namespaces_Historical_Reservations_And_Id_Only_References(
        string identity)
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var namespaceId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        if (identity == "reservation")
        {
            await ExecuteAsync("""INSERT INTO "ShortLinkNamespaceReservations" ("Id") VALUES ('History')""");
        }
        else
        {
            var domain = Guid.NewGuid();
            var tenant = Guid.NewGuid();
            const string origin = "https://namespace-history.example.test";
            await InsertDomainAsync(domain, tenant, origin);
            await InsertNamespaceAsync(namespaceId, tenant, domain, origin, "Team", _owner);
            if (identity == "namespace-id")
            {
                await InsertLinkAsync(linkId, tenant, domain, origin, "", namespaceId, deleted: true);
            }
        }

        var exception = await RejectAsync(() => context.GetService<IMigrator>()
            .MigrateAsync(ShortLinkNamespaceHostModelTests.PreviousMigration), "P0001");
        exception.MessageText.ShouldContain(identity switch
        {
            "namespace" => "user namespaces",
            "reservation" => "historical namespace reservations",
            _ => "namespaced short links"
        });
        await AssertNamespaceMigrationStillAppliedAsync(context);
        if (identity == "reservation")
        {
            (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ShortLinkNamespaceReservations" WHERE "Id" = 'History'"""))
                .ShouldBe(1);
            await ExecuteAsync("""DELETE FROM "ShortLinkNamespaceReservations" WHERE "Id" = 'History'""");
        }
        else
        {
            (await ScalarAsync<long>("""SELECT COUNT(*) FROM "ShortLinkNamespaces" WHERE "Id" = @id""",
                Parameter("id", NpgsqlDbType.Uuid, namespaceId))).ShouldBe(1);
            await ExecuteAsync("""DELETE FROM "ShortLinkLinks" WHERE "Id" = @id""",
                Parameter("id", NpgsqlDbType.Uuid, linkId));
            await ExecuteAsync("""DELETE FROM "ShortLinkNamespaces" WHERE "Id" = @id""",
                Parameter("id", NpgsqlDbType.Uuid, namespaceId));
        }
        await context.GetService<IMigrator>().MigrateAsync(ShortLinkNamespaceHostModelTests.PreviousMigration);
    }

    private static async Task AssertNamespaceMigrationStillAppliedAsync(WebHostDbContext context)
    {
        // EF Core commits each reverted migration separately, so later migrations are already reverted when this guard fails.
        (await context.Database.GetAppliedMigrationsAsync()).Last().ShouldBe(NamespaceMigration);
    }

    private Task InsertDomainAsync(Guid id, Guid? tenantId, string origin)
    {
        var domain = new ShortLinkDomain(id, tenantId, origin);
        return ExecuteAsync(
            """
            INSERT INTO "ShortLinkDomains"
                ("Id", "TenantId", "Origin", "IsEnabled", "IsDefault", "TenantScopeKey", "DefaultUniquenessKey",
                 "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            VALUES (@id, @tenant, @origin, TRUE, FALSE, @tenantScope, @defaultKey, '{}', @stamp, CURRENT_TIMESTAMP)
            """,
            Parameter("id", NpgsqlDbType.Uuid, id),
            Parameter("tenant", NpgsqlDbType.Uuid, tenantId),
            Parameter("origin", NpgsqlDbType.Text, origin),
            Parameter("tenantScope", NpgsqlDbType.Text, domain.TenantScopeKey),
            Parameter("defaultKey", NpgsqlDbType.Text, domain.DefaultUniquenessKey),
            Parameter("stamp", NpgsqlDbType.Text, Guid.NewGuid().ToString("N")));
    }

    private Task InsertNamespaceAsync(Guid id, Guid? tenantId, Guid domainId, string origin, string name, Guid owner) =>
        ExecuteAsync(
            """
            INSERT INTO "ShortLinkNamespaces"
                ("Id", "TenantId", "OwnerUserId", "DomainId", "Origin", "Name",
                 "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            VALUES (@id, @tenant, @owner, @domain, @origin, @name, '{}', @stamp, CURRENT_TIMESTAMP)
            """,
            Parameter("id", NpgsqlDbType.Uuid, id),
            Parameter("tenant", NpgsqlDbType.Uuid, tenantId),
            Parameter("owner", NpgsqlDbType.Uuid, owner),
            Parameter("domain", NpgsqlDbType.Uuid, domainId),
            Parameter("origin", NpgsqlDbType.Text, origin),
            Parameter("name", NpgsqlDbType.Text, name),
            Parameter("stamp", NpgsqlDbType.Text, Guid.NewGuid().ToString("N")));

    private Task InsertLinkAsync(Guid id, Guid? tenantId, Guid? domainId, string? origin, string pathNamespace,
        Guid? namespaceId, bool deleted = false) => ExecuteAsync(
        """
        INSERT INTO "ShortLinkLinks"
            ("Id", "TenantId", "OwnerUserId", "DomainId", "Origin", "Namespace", "NamespaceId",
             "Code", "TargetUrl", "Status", "IsCustomCode", "TotalVisitCount", "IsDeleted",
             "ExtraProperties", "ConcurrencyStamp", "CreationTime")
        VALUES (@id, @tenant, @owner, @domain, @origin, @namespace, @namespaceId,
            'SameCode', 'https://example.com/', 0, FALSE, 0, @deleted, '{}', @stamp, CURRENT_TIMESTAMP)
        """,
        Parameter("id", NpgsqlDbType.Uuid, id),
        Parameter("tenant", NpgsqlDbType.Uuid, tenantId),
        Parameter("owner", NpgsqlDbType.Uuid, _owner),
        Parameter("domain", NpgsqlDbType.Uuid, domainId),
        Parameter("origin", NpgsqlDbType.Text, origin),
        Parameter("namespace", NpgsqlDbType.Text, pathNamespace),
        Parameter("namespaceId", NpgsqlDbType.Uuid, namespaceId),
        Parameter("deleted", NpgsqlDbType.Boolean, deleted),
        Parameter("stamp", NpgsqlDbType.Text, Guid.NewGuid().ToString("N")));

    private Task InsertLegacySchemaLinkAsync(Guid id, Guid? tenantId, Guid? domainId, string? origin, bool deleted = false) =>
        ExecuteAsync(
            """
            INSERT INTO "ShortLinkLinks"
                ("Id", "TenantId", "OwnerUserId", "DomainId", "Origin", "Code", "TargetUrl", "Status",
                 "ExpiresAt", "IsCustomCode", "TotalVisitCount", "IsDeleted",
                 "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            VALUES (@id, @tenant, @owner, @domain, @origin, 'LegacyCode', 'https://example.com/', 1,
                TIMESTAMP '2000-01-01', TRUE, 7, @deleted, '{}', @stamp, CURRENT_TIMESTAMP)
            """,
            Parameter("id", NpgsqlDbType.Uuid, id),
            Parameter("tenant", NpgsqlDbType.Uuid, tenantId),
            Parameter("owner", NpgsqlDbType.Uuid, _owner),
            Parameter("domain", NpgsqlDbType.Uuid, domainId),
            Parameter("origin", NpgsqlDbType.Text, origin),
            Parameter("deleted", NpgsqlDbType.Boolean, deleted),
            Parameter("stamp", NpgsqlDbType.Text, Guid.NewGuid().ToString("N")));

    private Task<string> IdentitySnapshotAsync() => ScalarAsync<string>(
        """
        SELECT COALESCE(string_agg(row_to_json(rows)::text, E'\n' ORDER BY rows."Id"), '')
        FROM (
            SELECT "Id", "TenantId", "OwnerUserId", "DomainId", "Origin", "Code", "TargetUrl", "Status",
                   "ExpiresAt", "IsCustomCode", "TotalVisitCount", "IsDeleted", "ConcurrencyStamp", "CreationTime"
            FROM "ShortLinkLinks"
        ) rows
        """);

    private async Task<SeededProduct> InsertProductAsync(Guid? tenantId, string code, string name)
    {
        var product = new SeededProduct(Guid.NewGuid(), tenantId, code);
        await ExecuteAsync(
            """
            INSERT INTO "SubscriptionProducts"
                ("Id", "TenantId", "Code", "Name", "DisplayOrder", "State", "ExtraProperties", "ConcurrencyStamp",
                 "CreationTime")
            VALUES (@id, @tenant, @code, @name, 0, 1, '{}', 'product-' || @name, CURRENT_TIMESTAMP)
            """,
            Parameter("id", NpgsqlDbType.Uuid, product.Id),
            Parameter("tenant", NpgsqlDbType.Uuid, tenantId),
            Parameter("code", NpgsqlDbType.Text, code),
            Parameter("name", NpgsqlDbType.Text, name));
        return product;
    }

    private Task SeedNamespaceGrantAsync(SeededProduct product, string code, bool? legacyFlag, long? quota,
        bool chinese = false) => ExecuteAsync(
        """
        INSERT INTO "SubscriptionPlans"
            ("Id", "TenantId", "ProductId", "ProductCode", "Code", "Name", "DisplayOrder", "State",
             "ExtraProperties", "ConcurrencyStamp", "CreationTime")
        VALUES (@plan, @tenant, @product, @productCode, @code, @code, 0, 1, '{}', 'plan-' || @code, CURRENT_TIMESTAMP);
        INSERT INTO "SubscriptionPlanEntitlements"
            ("PlanId", "FeatureKey", "TenantId", "ValueType", "BooleanValue", "NumericValue", "IsUnlimited")
        VALUES
            (@plan, 'max-links', @tenant, 1, NULL, 100, FALSE),
            (@plan, 'max-namespaces', @tenant, 1, NULL, @quota, @quota IS NULL);
        INSERT INTO "SubscriptionPlanEntitlements"
            ("PlanId", "FeatureKey", "TenantId", "ValueType", "BooleanValue", "IsUnlimited")
        SELECT @plan, 'namespaces', @tenant, 0, @flag, FALSE
        WHERE @flag IS NOT NULL;
        INSERT INTO "SubscriptionUserSubscriptions"
            ("Id", "TenantId", "UserId", "ProductId", "SourcePlanId", "AssignmentId", "ProductCode", "ProductName",
             "PlanCode", "PlanName", "StartsAt", "IsCurrent", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
        VALUES (@subscription, @tenant, gen_random_uuid(), @product, @plan, gen_random_uuid(), @productCode,
            @productCode, @code, @code, CURRENT_TIMESTAMP, TRUE, '{}', 'subscription-' || @code, CURRENT_TIMESTAMP);
        INSERT INTO "SubscriptionUserSubscriptionEntitlements"
            ("SubscriptionId", "FeatureKey", "TenantId", "DisplayName", "ValueType", "BooleanValue", "NumericValue",
             "IsUnlimited")
        VALUES
            (@subscription, 'max-links', @tenant, @linksName, 1, NULL, 100, FALSE),
            (@subscription, 'max-namespaces', @tenant, @quotaName, 1, NULL, @quota, @quota IS NULL);
        INSERT INTO "SubscriptionUserSubscriptionEntitlements"
            ("SubscriptionId", "FeatureKey", "TenantId", "DisplayName", "ValueType", "BooleanValue", "IsUnlimited")
        SELECT @subscription, 'namespaces', @tenant, @flagName, 0, @flag, FALSE
        WHERE @flag IS NOT NULL;
        """,
        Parameter("plan", NpgsqlDbType.Uuid, Guid.NewGuid()),
        Parameter("subscription", NpgsqlDbType.Uuid, Guid.NewGuid()),
        Parameter("product", NpgsqlDbType.Uuid, product.Id),
        Parameter("tenant", NpgsqlDbType.Uuid, product.TenantId),
        Parameter("productCode", NpgsqlDbType.Text, product.Code),
        Parameter("code", NpgsqlDbType.Text, code),
        Parameter("flag", NpgsqlDbType.Boolean, legacyFlag),
        Parameter("quota", NpgsqlDbType.Bigint, quota),
        Parameter("linksName", NpgsqlDbType.Text, chinese ? "链接数量上限" : "Maximum number of links"),
        Parameter("quotaName", NpgsqlDbType.Text, chinese ? "命名空间数量上限" : "Maximum number of namespaces"),
        Parameter("flagName", NpgsqlDbType.Text, chinese ? "短链接命名空间" : "Short-link namespaces"));

    private Task<string[]> EntitlementStateAsync() => ScalarAsync<string[]>(
        """
        WITH entitlements AS (
            SELECT 'plan:' || plan."Code" AS holder, entitlement."FeatureKey", entitlement."ValueType",
                   entitlement."BooleanValue", entitlement."NumericValue", entitlement."IsUnlimited"
            FROM "SubscriptionPlans" AS plan
            JOIN "SubscriptionPlanEntitlements" AS entitlement ON entitlement."PlanId" = plan."Id"
            UNION ALL
            SELECT 'subscription:' || subscription."PlanCode", entitlement."FeatureKey", entitlement."ValueType",
                   entitlement."BooleanValue", entitlement."NumericValue", entitlement."IsUnlimited"
            FROM "SubscriptionUserSubscriptions" AS subscription
            JOIN "SubscriptionUserSubscriptionEntitlements" AS entitlement
                ON entitlement."SubscriptionId" = subscription."Id"
        ), lines AS (
            SELECT holder || ' ' || string_agg("FeatureKey" || '=' || CASE
                       WHEN "ValueType" = 0 THEN "BooleanValue"::text
                       WHEN "IsUnlimited" THEN 'unlimited'
                       ELSE "NumericValue"::text
                   END, ' ' ORDER BY "FeatureKey" COLLATE "C") AS line
            FROM entitlements
            GROUP BY holder
        )
        SELECT array_agg(line ORDER BY line COLLATE "C") FROM lines
        """);

    private async Task<Dictionary<string, string>> StampsAsync() =>
        (await ScalarAsync<string[]>(
            """
            SELECT array_agg(stamp)
            FROM (
                SELECT 'product:' || "Name" || '=' || "ConcurrencyStamp" AS stamp FROM "SubscriptionProducts"
                UNION ALL
                SELECT 'plan:' || "Code" || '=' || "ConcurrencyStamp" FROM "SubscriptionPlans"
                UNION ALL
                SELECT 'subscription:' || "PlanCode" || '=' || "ConcurrencyStamp" FROM "SubscriptionUserSubscriptions"
            ) AS stamps
            """))
        .Select(stamp => stamp.Split('='))
        .ToDictionary(parts => parts[0], parts => parts[1]);

    private static string[] RotatedStamps(Dictionary<string, string> before, Dictionary<string, string> after)
    {
        after.Keys.ShouldBe(before.Keys, ignoreOrder: true);
        var rotated = before.Keys.Where(key => after[key] != before[key]).Order(StringComparer.Ordinal).ToArray();
        foreach (var key in rotated)
        {
            after[key].ShouldMatch("^[0-9a-f]{32}$");
        }
        rotated.Select(key => after[key]).Distinct().Count().ShouldBe(rotated.Length);
        return rotated;
    }

    private async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static NpgsqlParameter Parameter(string name, NpgsqlDbType type, object? value) =>
        new(name, type) { Value = value ?? DBNull.Value };

    private static async Task<PostgresException> RejectAsync(
        Func<Task> action, string sqlState, string? constraint = null)
    {
        var exception = await Should.ThrowAsync<PostgresException>(action);
        exception.SqlState.ShouldBe(sqlState);
        if (constraint != null)
        {
            exception.ConstraintName.ShouldBe(constraint);
        }
        return exception;
    }

    private sealed record SeededProduct(Guid Id, Guid? TenantId, string Code);
}
