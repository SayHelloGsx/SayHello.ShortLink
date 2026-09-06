using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SayHello.Subscription.Entitlements;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Uow;
using Xunit;

namespace SayHello.Subscription.EntityFrameworkCore;

public class SubscriptionTransactionTests : SubscriptionPersistenceTestBase
{
    [Fact]
    public async Task In_process_preview_and_entitlement_queries_work_without_an_ambient_unit()
    {
        var data = await SeedAsync();
        var preview = await Manager.PreviewPlanAsync(null, data.UserId, data.Plans[0].Id);
        await Manager.AssignPlanAsync(new AssignSubscriptionPlan(null, data.UserId, Target(preview.Items[0])));
        var checker = GetRequiredService<ISubscriptionEntitlementChecker>();
        await checker.RequireBooleanAsync(null, data.UserId, "alpha", "enabled");
        Assert.Equal(10, (await checker.GetNumericAsync(null, data.UserId, "alpha", "limit")).Limit);
        Assert.Null(GetRequiredService<IUnitOfWorkManager>().Current);
    }

    [Fact]
    public async Task Standalone_assignment_owns_a_transaction_and_releases_its_lock()
    {
        var data = await SeedAsync();
        var preview = await InTransactionAsync(() => Manager.PreviewPlanAsync(null, data.UserId, data.Plans[0].Id));
        var assigned = await Manager.AssignPlanAsync(new AssignSubscriptionPlan(null, data.UserId, Target(preview.Items[0])));
        Assert.Null(GetRequiredService<IUnitOfWorkManager>().Current);
        Assert.Equal(0, GetRequiredService<SubscriptionTestDistributedLock>().HeldCount);
        await InTransactionAsync(async () =>
        {
            Assert.Equal(assigned.Id, (await Subscriptions.FindCurrentAsync(null, data.UserId, data.Products[0].Id))!.Id);
            return true;
        });
    }

    [Fact]
    public async Task Assignment_and_expiration_change_can_share_one_ambient_transaction()
    {
        var data = await SeedAsync();
        await InTransactionAsync(async () =>
        {
            var preview = await Manager.PreviewPlanAsync(null, data.UserId, data.Plans[0].Id);
            var assigned = await Manager.AssignPlanAsync(new AssignSubscriptionPlan(null, data.UserId, Target(preview.Items[0])));
            var adjusted = await Manager.AdjustExpirationAsync(null, assigned.Id, assigned.ConcurrencyStamp, TestClock.Now.AddHours(1));
            Assert.Equal(TestClock.Now.AddHours(1), adjusted.ExpiresAt);
            Assert.Equal(1, GetRequiredService<SubscriptionTestDistributedLock>().HeldCount);
            return true;
        });
        Assert.Equal(0, GetRequiredService<SubscriptionTestDistributedLock>().HeldCount);
    }

    [Fact]
    public async Task Failure_in_second_bundle_insert_rolls_back_replacements_and_first_insert()
    {
        var data = await SeedAsync();
        var originals = await AssignBundleAsync(data, data.AB);
        await using (var connection = new SqliteConnection(GetRequiredService<SubscriptionTestDatabase>().ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER FailBetaAssignment BEFORE INSERT ON "SubscriptionUserSubscriptions"
                WHEN NEW."ProductCode" = 'beta'
                BEGIN SELECT RAISE(ABORT, 'forced second component failure'); END;
                """;
            await command.ExecuteNonQueryAsync();
        }
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => AssignBundleAsync(data, data.AB));
        Assert.Contains("forced second component failure", exception.InnerException!.Message);
        await InTransactionAsync(async () =>
        {
            var current = await Subscriptions.GetCurrentListAsync(null, data.UserId);
            Assert.Equal(originals.Select(x => x.Id).Order(), current.Select(x => x.Id).Order());
            Assert.All(current, x => { Assert.True(x.IsCurrent); Assert.Null(x.EndedAt); });
            Assert.Equal(2, (await Subscriptions.GetPageAsync(new UserSubscriptionQuery(null, TestClock.Now, data.UserId))).TotalCount);
            return true;
        });
        Assert.Equal(0, GetRequiredService<SubscriptionTestDistributedLock>().HeldCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unique_current_slot_is_enforced_by_database_on_separate_connections(bool hasTenant)
    {
        var tenantId = hasTenant ? Guid.NewGuid() : (Guid?)null;
        var data = await SeedAsync(tenantId);
        var original = await AssignPlanAsync(data, 0);
        var connectionString = GetRequiredService<SubscriptionTestDatabase>().ConnectionString;
        await using var observer = new SqliteConnection(connectionString);
        await using var writer = new SqliteConnection(connectionString);
        await observer.OpenAsync();
        await writer.OpenAsync();
        Assert.NotSame(observer, writer);
        var columns = new List<string>();
        await using (var metadata = observer.CreateCommand())
        {
            metadata.CommandText = """PRAGMA table_info("SubscriptionUserSubscriptions");""";
            await using var reader = await metadata.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(1);
                if (name != "Id") columns.Add("\"" + name + "\"");
            }
        }
        await using (var transaction = await writer.BeginTransactionAsync())
        {
            await using var duplicate = writer.CreateCommand();
            duplicate.Transaction = (SqliteTransaction)transaction;
            var names = string.Join(", ", columns);
            duplicate.CommandText = $"""
                INSERT INTO "SubscriptionUserSubscriptions" ("Id", {names})
                SELECT $newId, {names} FROM "SubscriptionUserSubscriptions" WHERE "Id" = $originalId;
                """;
            duplicate.Parameters.AddWithValue("$newId", Guid.NewGuid());
            duplicate.Parameters.AddWithValue("$originalId", original.Id);
            var exception = await Assert.ThrowsAsync<SqliteException>(() => duplicate.ExecuteNonQueryAsync());
            Assert.Equal(2067, exception.SqliteExtendedErrorCode);
            await transaction.RollbackAsync();
        }
        await using var count = observer.CreateCommand();
        count.CommandText = """SELECT COUNT(*) FROM "SubscriptionUserSubscriptions" WHERE "IsCurrent" = TRUE;""";
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Simultaneous_assignments_have_one_winner_and_one_stale_preview_failure()
    {
        var data = await SeedAsync();
        var preview = await InTransactionAsync(() => Manager.PreviewPlanAsync(null, data.UserId, data.Plans[0].Id));
        var input = new AssignSubscriptionPlan(null, data.UserId, Target(preview.Items[0]));
        async Task<string> Attempt()
        {
            try
            {
                await InTransactionAsync(() => Manager.AssignPlanAsync(input));
                return "assigned";
            }
            catch (BusinessException exception)
            {
                return exception.Code!;
            }
        }
        var outcomes = await Task.WhenAll(Task.Run(Attempt), Task.Run(Attempt));
        Assert.Single(outcomes, x => x == "assigned");
        Assert.Single(outcomes, x => x == SubscriptionErrorCodes.ConcurrencyConflict);
        await InTransactionAsync(async () =>
        {
            Assert.Single(await Subscriptions.GetCurrentListAsync(null, data.UserId));
            Assert.Equal(1, (await Subscriptions.GetPageAsync(new UserSubscriptionQuery(null, TestClock.Now, data.UserId))).TotalCount);
            return true;
        });
        Assert.Equal(0, GetRequiredService<SubscriptionTestDistributedLock>().HeldCount);
    }
}
