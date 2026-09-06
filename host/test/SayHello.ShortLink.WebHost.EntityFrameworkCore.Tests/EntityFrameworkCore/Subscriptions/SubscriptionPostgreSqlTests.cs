using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SayHello.ShortLink.WebHost.Data;
using SayHello.ShortLink.WebHost.Subscriptions;
using SayHello.Subscription;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Definitions;
using SayHello.Subscription.Subscriptions;
using SayHello.Subscription.Users;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

[Collection(SubscriptionPostgreSqlCollection.Name)]
public sealed class SubscriptionPostgreSqlTests : IAsyncLifetime
{
    private const string InitialMigration = "20260904045608_Initial";
    private const string SubscriptionMigration = "20260906110237_AddSubscriptions";
    private readonly SubscriptionPostgreSqlDatabase _database = new();
    private IAbpApplicationWithInternalServiceProvider? _application;
    private bool _initialized;

    public Task InitializeAsync() => _database.CreateAsync();

    public async Task DisposeAsync()
    {
        try
        {
            if (_initialized) await _application!.ShutdownAsync();
        }
        finally
        {
            try
            {
                _application?.Dispose();
            }
            finally
            {
                await _database.DisposeAsync();
            }
        }
    }

    [PostgreSqlFact]
    public async Task Initial_then_AddSubscriptions_and_reapply_preserve_baseline_schema_and_rows()
    {
        await using var context = _database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(InitialMigration);
        Assert.Equal(new[] { InitialMigration }, await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0L, await ScalarAsync("""
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name LIKE 'Subscription%';
            """));

        await ExecuteAsync("""
            INSERT INTO "AbpSettings" ("Id", "Name", "Value")
            VALUES ('10000000-0000-0000-0000-000000000001', 'Subscription.PostgreSql.Sentinel', 'preserve me');
            INSERT INTO "ShortLinkBlockedDomains"
                ("Id", "Domain", "Reason", "IsActive", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
            VALUES ('10000000-0000-0000-0000-000000000002', 'baseline.example', 'preserve me',
                TRUE, '{}', 'baseline-stamp', TIMESTAMP '2026-09-01 12:00:00');
            """);
        var baselineColumns = await BaselineColumnsAsync();
        var baselineSettings = await SnapshotAsync("AbpSettings");
        var baselineDomains = await SnapshotAsync("ShortLinkBlockedDomains");

        await migrator.MigrateAsync(SubscriptionMigration);
        Assert.Equal(new[] { InitialMigration, SubscriptionMigration }, await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(7L, await ScalarAsync("""
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name LIKE 'Subscription%';
            """));
        Assert.Equal(baselineColumns, await BaselineColumnsAsync());
        Assert.Equal(baselineSettings, await SnapshotAsync("AbpSettings"));
        Assert.Equal(baselineDomains, await SnapshotAsync("ShortLinkBlockedDomains"));

        var migrationHistory = await SnapshotAsync("__EFMigrationsHistory");
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(migrationHistory, await SnapshotAsync("__EFMigrationsHistory"));
        Assert.Equal(baselineColumns, await BaselineColumnsAsync());
        Assert.Equal(baselineSettings, await SnapshotAsync("AbpSettings"));
        Assert.Equal(baselineDomains, await SnapshotAsync("ShortLinkBlockedDomains"));
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Host_seed_identity_and_bundle_replacement_change_only_affected_product(bool hasTenant)
    {
        var data = await SeedCatalogAsync(hasTenant);
        var originals = await AssignBundleAsync(data);
        var unrelated = await AssignPlanAsync(data, 2);
        var replacement = await AssignPlanAsync(data, 0);

        var stored = await ReadSubscriptionsAsync(data);
        Assert.Equal(4, stored.Count);
        Assert.Equal(3, stored.Count(subscription => subscription.IsCurrent));
        var retired = stored.Single(subscription => subscription.Id == originals.Single(x => x.ProductId == data.Plans[0].ProductId).Id);
        Assert.False(retired.IsCurrent);
        Assert.Equal(SubscriptionEndReason.Replaced, retired.EndReason);
        Assert.NotNull(retired.EndedAt);
        Assert.All(stored.Where(subscription => subscription.Id != retired.Id), subscription =>
        {
            Assert.True(subscription.IsCurrent);
            Assert.Null(subscription.EndedAt);
        });
        var retained = originals.Single(subscription => subscription.ProductId == data.Plans[1].ProductId);
        Assert.Equal(retained.ConcurrencyStamp, stored.Single(subscription => subscription.Id == retained.Id).ConcurrencyStamp);
        Assert.Equal(unrelated.ConcurrencyStamp, stored.Single(subscription => subscription.Id == unrelated.Id).ConcurrencyStamp);
        Assert.Equal(originals[0].AssignmentId, originals[1].AssignmentId);
        Assert.NotEqual(originals[0].AssignmentId, replacement.AssignmentId);
        Assert.All(originals, subscription => Assert.Equal(data.BundleId, subscription.SourceBundleId));
        Assert.Null(replacement.SourceBundleId);
        Assert.Equal(2, stored.Single(subscription => subscription.Id == replacement.Id).Entitlements.Count);
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Second_bundle_component_database_failure_rolls_back_retirements_inserts_and_snapshots(bool hasTenant)
    {
        var data = await SeedCatalogAsync(hasTenant);
        await AssignBundleAsync(data);
        await AssignPlanAsync(data, 2);
        var subscriptionsBefore = await SnapshotAsync("SubscriptionUserSubscriptions");
        var snapshotsBefore = await SnapshotAsync("SubscriptionUserSubscriptionEntitlements");
        await ExecuteAsync("""
            CREATE FUNCTION subscription_test_fail_second_component() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."ProductCode" = 'short-link' THEN
                    IF NOT EXISTS (
                        SELECT 1 FROM "SubscriptionUserSubscriptions"
                        WHERE "ProductCode" = 'postgres-beta' AND "AssignmentId" = NEW."AssignmentId"
                    ) THEN
                        RAISE EXCEPTION 'first component was not persisted';
                    END IF;
                    RAISE EXCEPTION 'forced second component failure';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER subscription_test_fail_second_component
                BEFORE INSERT ON "SubscriptionUserSubscriptions"
                FOR EACH ROW EXECUTE FUNCTION subscription_test_fail_second_component();
            """);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => AssignBundleAsync(data));
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.RaiseException, postgres.SqlState);
        Assert.Equal("forced second component failure", postgres.MessageText);
        Assert.Equal(subscriptionsBefore, await SnapshotAsync("SubscriptionUserSubscriptions"));
        Assert.Equal(snapshotsBefore, await SnapshotAsync("SubscriptionUserSubscriptionEntitlements"));
        Assert.All(await ReadSubscriptionsAsync(data), subscription =>
        {
            Assert.True(subscription.IsCurrent);
            Assert.Null(subscription.EndedAt);
        });

        await ExecuteAsync("""DROP TRIGGER subscription_test_fail_second_component ON "SubscriptionUserSubscriptions";""");
        Assert.Equal(2, (await AssignBundleAsync(data)).Count);
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_slot_unique_index_rejects_duplicate_from_an_independent_connection(bool hasTenant)
    {
        var data = await SeedCatalogAsync(hasTenant);
        var original = await AssignPlanAsync(data, 0);
        var columns = (string)(await ScalarAsync("""
            SELECT string_agg(quote_ident(column_name), ', ' ORDER BY ordinal_position)
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'SubscriptionUserSubscriptions' AND column_name <> 'Id';
            """))!;
        await using var observer = new NpgsqlConnection(_database.ConnectionString);
        await using var writer = new NpgsqlConnection(_database.ConnectionString);
        await observer.OpenAsync();
        await writer.OpenAsync();
        Assert.NotEqual(observer.ProcessID, writer.ProcessID);
        await using (var transaction = await writer.BeginTransactionAsync())
        {
            await using var duplicate = writer.CreateCommand();
            duplicate.Transaction = transaction;
            duplicate.CommandText = $"""
                INSERT INTO "SubscriptionUserSubscriptions" ("Id", {columns})
                SELECT @newId, {columns} FROM "SubscriptionUserSubscriptions" WHERE "Id" = @originalId;
                """;
            duplicate.Parameters.AddWithValue("newId", Guid.NewGuid());
            duplicate.Parameters.AddWithValue("originalId", original.Id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => duplicate.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            Assert.Equal(hasTenant ? "UX_Subscription_Current_Tenant" : "UX_Subscription_Current_Host", error.ConstraintName);
            await transaction.RollbackAsync();
        }

        await using var count = observer.CreateCommand();
        count.CommandText = """SELECT count(*) FROM "SubscriptionUserSubscriptions" WHERE "IsCurrent";""";
        Assert.Equal(1L, await count.ExecuteScalarAsync());
        await AssignPlanAsync(data, 0);
        var history = await ReadSubscriptionsAsync(data);
        Assert.Equal(2, history.Count);
        Assert.Single(history, subscription => subscription.IsCurrent);
    }

    [PostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lifecycle_timestamps_roundtrip_with_UTC_kind_and_Z_under_host_legacy_Npgsql(bool permanent)
    {
        var data = await SeedCatalogAsync(false);
        Assert.True(AppContext.TryGetSwitch("Npgsql.EnableLegacyTimestampBehavior", out var legacy) && legacy);
        var clock = _application!.ServiceProvider.GetRequiredService<SubscriptionPostgreSqlClock>();
        Assert.Equal(DateTimeKind.Unspecified, clock.Kind);
        clock.Now = DateTime.SpecifyKind(clock.Now, DateTimeKind.Local);
        var startsAt = clock.Now.ToUniversalTime();
        var expiresAt = permanent ? (DateTime?)null : startsAt.AddHours(1);
        var assigned = await AssignPlanAsync(data, 0, expiresAt);
        var current = Assert.Single(await ReadSubscriptionsAsync(data));
        Assert.Equal(startsAt, current.StartsAt);
        Assert.Equal(DateTimeKind.Utc, current.StartsAt.Kind);
        Assert.Equal(expiresAt, current.ExpiresAt);
        Assert.Null(current.EndedAt);
        Assert.Equal(DateTimeKind.Unspecified,
            ((DateTime)(await ScalarAsync("""SELECT "StartsAt" FROM "SubscriptionUserSubscriptions";"""))!).Kind);

        clock.Now = clock.Now.AddMinutes(15);
        await InUnitAsync(data.TenantId, services =>
            services.GetRequiredService<ISubscriptionManager>().RevokeAsync(
                data.TenantId, assigned.Id, current.ConcurrencyStamp, "PostgreSQL timestamp test"));
        var history = Assert.Single(await ReadSubscriptionsAsync(data));
        Assert.Equal(startsAt, history.StartsAt);
        Assert.Equal(expiresAt, history.ExpiresAt);
        Assert.Equal(clock.Now.ToUniversalTime(), history.EndedAt);
        Assert.Equal(DateTimeKind.Utc, history.StartsAt.Kind);
        Assert.Equal(DateTimeKind.Utc, history.EndedAt!.Value.Kind);
        if (!permanent) Assert.Equal(DateTimeKind.Utc, history.ExpiresAt!.Value.Kind);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { history.StartsAt, history.ExpiresAt, history.EndedAt }));
        Assert.EndsWith("Z", json.RootElement.GetProperty("StartsAt").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("Z", json.RootElement.GetProperty("EndedAt").GetString(), StringComparison.Ordinal);
        if (permanent)
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("ExpiresAt").ValueKind);
        else
            Assert.EndsWith("Z", json.RootElement.GetProperty("ExpiresAt").GetString(), StringComparison.Ordinal);
    }

    private async Task InitializeHostAsync()
    {
        await using (var context = _database.CreateContext())
            await context.Database.MigrateAsync();
        _application = await AbpApplicationFactory.CreateAsync<SubscriptionPostgreSqlTestModule>(options =>
        {
            options.UseAutofac();
            options.Services.Configure<AbpDbConnectionOptions>(connections =>
                connections.ConnectionStrings.Default = _database.ConnectionString);
        });
        await _application.InitializeAsync();
        _initialized = true;
    }

    private async Task<CatalogData> SeedCatalogAsync(bool hasTenant)
    {
        await InitializeHostAsync();
        var tenantId = hasTenant ? Guid.NewGuid() : (Guid?)null;
        return await InUnitAsync(tenantId, async services =>
        {
            var users = services.GetRequiredService<IIdentityUserRepository>();
            var user = await users.InsertAsync(new IdentityUser(Guid.NewGuid(), "postgres-user", "postgres@example.test", tenantId), true);
            var directory = services.GetRequiredService<ISubscriptionUserDirectory>();
            Assert.IsAssignableFrom<IdentitySubscriptionUserDirectory>(directory);
            Assert.Equal(user.Id, (await directory.FindAsync(tenantId, user.Id))!.Id);

            var seed = services.GetRequiredService<SubscriptionProductDataSeedContributor>();
            var products = services.GetRequiredService<ISubscriptionProductRepository>();
            var catalog = services.GetRequiredService<ISubscriptionCatalogManager>();
            await seed.SeedAsync(new DataSeedContext(tenantId));
            await seed.SeedAsync(new DataSeedContext(tenantId));
            var product = (await products.FindByCodeAsync(tenantId, ShortLinkSubscriptionDefinitions.ProductCode))!;
            Assert.Equal(SubscriptionCatalogState.Draft, product.State);
            var plans = new List<SubscriptionPlan>();
            foreach (var code in new[] { ShortLinkSubscriptionDefinitions.ProductCode, "postgres-beta", "postgres-gamma" })
            {
                if (code != ShortLinkSubscriptionDefinitions.ProductCode)
                    product = await catalog.CreateProductAsync(tenantId, code, new CatalogDetails(code));
                product = await catalog.SetProductStateAsync(tenantId, product.Id, product.ConcurrencyStamp, SubscriptionCatalogState.Published);
                var values = code == ShortLinkSubscriptionDefinitions.ProductCode
                    ? new Dictionary<string, EntitlementValue>
                    {
                        [ShortLinkSubscriptionDefinitions.Statistics] = EntitlementValue.Boolean(true),
                        [ShortLinkSubscriptionDefinitions.MaxLinks] = EntitlementValue.Numeric(25)
                    }
                    : new Dictionary<string, EntitlementValue> { ["enabled"] = EntitlementValue.Boolean(true) };
                var plan = await catalog.CreatePlanAsync(tenantId, product.Id, "basic", new CatalogDetails("Basic"), values);
                plans.Add(await catalog.SetPlanStateAsync(tenantId, plan.Id, plan.ConcurrencyStamp, SubscriptionCatalogState.Published));
            }

            var shortLink = (await products.FindByCodeAsync(tenantId, ShortLinkSubscriptionDefinitions.ProductCode))!;
            shortLink = await catalog.UpdateProductAsync(tenantId, shortLink.Id, shortLink.ConcurrencyStamp, new CatalogDetails("Administrator name"));
            var stamp = shortLink.ConcurrencyStamp;
            await seed.SeedAsync(new DataSeedContext(tenantId));
            shortLink = (await products.FindByCodeAsync(tenantId, ShortLinkSubscriptionDefinitions.ProductCode))!;
            Assert.Equal("Administrator name", shortLink.Name);
            Assert.Equal(SubscriptionCatalogState.Published, shortLink.State);
            Assert.Equal(stamp, shortLink.ConcurrencyStamp);

            var bundle = await catalog.CreateBundleAsync(tenantId, "postgres-bundle", new CatalogDetails("PostgreSQL bundle"),
                plans.Take(2).Select(plan => plan.Id).ToArray());
            bundle = await catalog.SetBundleStateAsync(tenantId, bundle.Id, bundle.ConcurrencyStamp, SubscriptionCatalogState.Published);
            return new CatalogData(tenantId, user.Id, plans.ToArray(), bundle.Id);
        });
    }

    private Task<UserSubscription> AssignPlanAsync(CatalogData data, int planIndex, DateTime? expiresAt = null) =>
        InUnitAsync(data.TenantId, async services =>
        {
            var manager = services.GetRequiredService<ISubscriptionManager>();
            var preview = await manager.PreviewPlanAsync(data.TenantId, data.UserId, data.Plans[planIndex].Id);
            return await manager.AssignPlanAsync(new AssignSubscriptionPlan(data.TenantId, data.UserId,
                Target(preview.Items[0], expiresAt)));
        });

    private Task<IReadOnlyList<UserSubscription>> AssignBundleAsync(CatalogData data) =>
        InUnitAsync(data.TenantId, async services =>
        {
            var manager = services.GetRequiredService<ISubscriptionManager>();
            var preview = await manager.PreviewBundleAsync(data.TenantId, data.UserId, data.BundleId);
            return await manager.AssignBundleAsync(new AssignSubscriptionBundle(data.TenantId, data.UserId,
                data.BundleId, preview.BundleConcurrencyStamp!, preview.Items.Select(item => Target(item))));
        });

    private Task<IReadOnlyList<UserSubscription>> ReadSubscriptionsAsync(CatalogData data) =>
        InUnitAsync<IReadOnlyList<UserSubscription>>(data.TenantId, async services =>
            (await services.GetRequiredService<IUserSubscriptionRepository>().GetPageAsync(
                new UserSubscriptionQuery(data.TenantId, services.GetRequiredService<SubscriptionPostgreSqlClock>().Now.ToUniversalTime(),
                    data.UserId))).Items);

    private async Task<T> InUnitAsync<T>(Guid? tenantId, Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _application!.ServiceProvider.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
            .Begin(requiresNew: true, isTransactional: true);
        var result = await action(scope.ServiceProvider);
        await unit.CompleteAsync();
        return result;
    }

    private static SubscriptionAssignmentTarget Target(SubscriptionAssignmentPreviewItem item, DateTime? expiresAt = null) =>
        new(item.ProductId, item.PlanId, item.ProductConcurrencyStamp, item.PlanConcurrencyStamp, expiresAt, item.ExpectedCurrent);

    private Task<object?> BaselineColumnsAsync() => ScalarAsync("""
        SELECT string_agg(concat_ws('|', table_name, column_name, data_type, is_nullable, column_default),
            E'\n' ORDER BY table_name, ordinal_position)
        FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name NOT LIKE 'Subscription%' AND table_name <> '__EFMigrationsHistory';
        """);

    private Task<object?> SnapshotAsync(string table) => ScalarAsync($"""
        SELECT coalesce(jsonb_agg(to_jsonb(row) ORDER BY to_jsonb(row)::text), '[]'::jsonb)::text
        FROM "{table}" AS row;
        """);

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record CatalogData(Guid? TenantId, Guid UserId, SubscriptionPlan[] Plans, Guid BundleId);
}
