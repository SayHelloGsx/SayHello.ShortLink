using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.WebHost.Migrations;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;
using Shouldly;
using Xunit;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.Subscriptions;

public class SubscriptionHostModelTests
{
    [Fact]
    public void Host_Model_Should_Include_All_Subscription_Tables_Without_Identity_Foreign_Keys()
    {
        using var context = CreateContext();
        var types = new[]
        {
            typeof(SubscriptionProduct), typeof(SubscriptionPlan), typeof(SubscriptionPlanEntitlement),
            typeof(SubscriptionBundle), typeof(SubscriptionBundleItem),
            typeof(UserSubscription), typeof(UserSubscriptionEntitlement)
        };

        foreach (var type in types)
        {
            var entity = context.Model.FindEntityType(type);
            entity.ShouldNotBeNull();
            entity.GetTableName().ShouldStartWith("Subscription");
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.PrincipalEntityType.ClrType.Assembly
                    .ShouldBe(typeof(SubscriptionProduct).Assembly);
            }
        }
    }

    [Fact]
    public void Current_Subscription_Uniqueness_Should_Cover_Host_And_Tenant_Users()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(UserSubscription));
        entity.ShouldNotBeNull();
        var indexes = entity.GetIndexes().Where(index => index.IsUnique).ToList();

        var host = indexes.Single(index => index.GetDatabaseName() == "UX_Subscription_Current_Host");
        host.Properties.Select(property => property.Name).ShouldBe(
            new[] { nameof(UserSubscription.UserId), nameof(UserSubscription.ProductId) });
        host.GetFilter().ShouldBe("\"TenantId\" IS NULL AND \"IsCurrent\" = TRUE");

        var tenant = indexes.Single(index => index.GetDatabaseName() == "UX_Subscription_Current_Tenant");
        tenant.Properties.Select(property => property.Name).ShouldBe(
            new[] { nameof(UserSubscription.TenantId), nameof(UserSubscription.UserId), nameof(UserSubscription.ProductId) });
        tenant.GetFilter().ShouldBe("\"TenantId\" IS NOT NULL AND \"IsCurrent\" = TRUE");
    }

    [Fact]
    public void Default_Plan_Should_Be_Optional_And_Restricted_To_The_Same_Product()
    {
        using var context = CreateContext();
        var product = context.Model.FindEntityType(typeof(SubscriptionProduct))!;
        var property = product.FindProperty(nameof(SubscriptionProduct.DefaultPlanId))!;
        property.ClrType.ShouldBe(typeof(Guid?));
        property.IsNullable.ShouldBeTrue();

        var foreignKey = product.GetForeignKeys().Single(key =>
            key.Properties.Any(value => value.Name == nameof(SubscriptionProduct.DefaultPlanId)));
        foreignKey.Properties.Select(value => value.Name)
            .ShouldBe(new[] { nameof(SubscriptionProduct.DefaultPlanId), nameof(SubscriptionProduct.Id) });
        foreignKey.PrincipalEntityType.ClrType.ShouldBe(typeof(SubscriptionPlan));
        foreignKey.PrincipalKey.Properties.Select(value => value.Name)
            .ShouldBe(new[] { nameof(SubscriptionPlan.Id), nameof(SubscriptionPlan.ProductId) });
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
        foreignKey.IsRequired.ShouldBeFalse();
        product.GetIndexes().ShouldContain(index =>
            index.Properties.Select(value => value.Name)
                .SequenceEqual(new[] { nameof(SubscriptionProduct.DefaultPlanId), nameof(SubscriptionProduct.Id) }));
    }

    [Fact]
    public void Default_Plan_Migration_Should_Only_Add_The_Nullable_Pointer_Index_And_Restrictive_Foreign_Key()
    {
        var migration = new AddSubscriptionDefaultPlans();
        migration.UpOperations.Count.ShouldBe(3);
        var column = migration.UpOperations.OfType<AddColumnOperation>().Single();
        column.Table.ShouldBe("SubscriptionProducts");
        column.Name.ShouldBe(nameof(SubscriptionProduct.DefaultPlanId));
        column.ClrType.ShouldBe(typeof(Guid));
        column.IsNullable.ShouldBeTrue();
        column.DefaultValue.ShouldBeNull();
        column.DefaultValueSql.ShouldBeNull();

        var index = migration.UpOperations.OfType<CreateIndexOperation>().Single();
        index.Table.ShouldBe(column.Table);
        index.Columns.ShouldBe(new[] { "DefaultPlanId", "Id" });
        var foreignKey = migration.UpOperations.OfType<AddForeignKeyOperation>().Single();
        foreignKey.Table.ShouldBe(column.Table);
        foreignKey.Columns.ShouldBe(index.Columns);
        foreignKey.PrincipalTable.ShouldBe("SubscriptionPlans");
        foreignKey.PrincipalColumns.ShouldBe(new[] { "Id", "ProductId" });
        foreignKey.OnDelete.ShouldBe(ReferentialAction.Restrict);
        migration.DownOperations.Select(operation => operation.GetType()).ShouldBe(
            new[] { typeof(DropForeignKeyOperation), typeof(DropIndexOperation), typeof(DropColumnOperation) });
    }

    [Fact]
    public void Host_Model_Should_Use_Origin_Code_Identity_And_Restrict_Domain_Deletion()
    {
        using var context = CreateContext();
        var link = context.Model.FindEntityType(
            typeof(global::SayHello.ShortLink.ShortLinks.ShortLink))!;
        var domain = context.Model.FindEntityType(typeof(ShortLinkDomain))!;
        link.FindProperty("DomainId")!.IsNullable.ShouldBeTrue();
        link.FindProperty("Origin")!.IsNullable.ShouldBeTrue();

        var foreignKey = link.GetForeignKeys().Single(key =>
            key.PrincipalEntityType == domain);
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
        foreignKey.Properties.ShouldHaveSingleItem().Name.ShouldBe("DomainId");
        link.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "Origin", "Code" }))
            .IsUnique.ShouldBeTrue();
        domain.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantScopeKey", "Origin" }))
            .IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void Entitlement_And_Domain_Migration_Should_Convert_Legacy_Statistics_Before_New_Constraints()
    {
        var migration = new AddEntitlementValuesAndShortLinkDomains();
        migration.UpOperations.OfType<AddColumnOperation>()
            .Count(operation => operation.Name is "StringValue" or "StringSetValue")
            .ShouldBe(4);
        migration.UpOperations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "ShortLinkDomains");
        migration.UpOperations.OfType<AddForeignKeyOperation>()
            .Single(operation => operation.PrincipalTable == "ShortLinkDomains")
            .OnDelete.ShouldBe(ReferentialAction.Restrict);
        migration.UpOperations.OfType<AddCheckConstraintOperation>().Count()
            .ShouldBe(2);

        var conversion = migration.UpOperations.OfType<SqlOperation>()
            .Single().Sql;
        conversion.ShouldContain("'short-link'");
        conversion.ShouldContain("'statistics'");
        conversion.ShouldContain("'basic'");
        conversion.ShouldContain("'advanced'");
        var operations = migration.UpOperations.ToList();
        operations.IndexOf(migration.UpOperations.OfType<SqlOperation>().Single())
            .ShouldBeLessThan(operations.IndexOf(
                migration.UpOperations.OfType<AddCheckConstraintOperation>().First()));
    }

    [Fact]
    public void Entitlement_And_Domain_Migration_Down_Should_Convert_Known_Statistics_Guard_New_Values_And_Order_Drops()
    {
        var migration = new AddEntitlementValuesAndShortLinkDomains();
        var operations = migration.DownOperations.ToList();
        var conversion = operations.OfType<SqlOperation>().Single();
        var conversionIndex = operations.IndexOf(conversion);
        conversion.SuppressTransaction.ShouldBeFalse();

        var droppedConstraints = operations.OfType<DropCheckConstraintOperation>().ToList();
        droppedConstraints.Select(operation => operation.Name).ShouldBe(
            ["CK_Subscription_Snapshot_Value", "CK_Subscription_PlanEntitlement_Value"],
            ignoreOrder: true);
        droppedConstraints
            .All(operation => operations.IndexOf(operation) < conversionIndex)
            .ShouldBeTrue();
        var droppedValueColumns = operations.OfType<DropColumnOperation>()
            .Where(operation => operation.Name is "StringValue" or "StringSetValue")
            .ToList();
        droppedValueColumns.Count.ShouldBe(4);
        droppedValueColumns.Count(operation =>
            operation.Table == "SubscriptionPlanEntitlements").ShouldBe(2);
        droppedValueColumns.Count(operation =>
            operation.Table == "SubscriptionUserSubscriptionEntitlements").ShouldBe(2);
        droppedValueColumns
            .All(operation => operations.IndexOf(operation) > conversionIndex)
            .ShouldBeTrue();

        var planUpdateIndex = conversion.Sql.IndexOf(
            """UPDATE "SubscriptionPlanEntitlements" AS entitlement""",
            StringComparison.Ordinal);
        var snapshotUpdateIndex = conversion.Sql.IndexOf(
            """UPDATE "SubscriptionUserSubscriptionEntitlements" AS entitlement""",
            StringComparison.Ordinal);
        var guardIndex = conversion.Sql.IndexOf("DO $migration$", StringComparison.Ordinal);
        planUpdateIndex.ShouldBeGreaterThanOrEqualTo(0);
        snapshotUpdateIndex.ShouldBeGreaterThan(planUpdateIndex);
        guardIndex.ShouldBeGreaterThan(snapshotUpdateIndex);

        var planUpdate = conversion.Sql[planUpdateIndex..snapshotUpdateIndex];
        var snapshotUpdate = conversion.Sql[snapshotUpdateIndex..guardIndex];
        foreach (var update in new[] { planUpdate, snapshotUpdate })
        {
            update.ShouldContain("\"ValueType\" = 0");
            update.ShouldContain("\"BooleanValue\" = entitlement.\"StringValue\" = 'advanced'");
            update.ShouldContain("\"StringValue\" = NULL");
            update.ShouldContain("\"FeatureKey\" = 'statistics'");
            update.ShouldContain("\"ValueType\" = 2");
            update.ShouldContain("\"StringValue\" IN ('none', 'basic', 'advanced')");
        }
        planUpdate.ShouldContain("""plan."ProductCode" = 'short-link'""");
        snapshotUpdate.ShouldContain("""subscription."ProductCode" = 'short-link'""");

        var guard = conversion.Sql[guardIndex..];
        var planGuardIndex = guard.IndexOf(
            "FROM \"SubscriptionPlanEntitlements\"",
            StringComparison.Ordinal);
        var snapshotGuardIndex = guard.IndexOf(
            "FROM \"SubscriptionUserSubscriptionEntitlements\"",
            StringComparison.Ordinal);
        planGuardIndex.ShouldBeGreaterThanOrEqualTo(0);
        snapshotGuardIndex.ShouldBeGreaterThan(planGuardIndex);
        guard[planGuardIndex..snapshotGuardIndex].ShouldContain("\"ValueType\" IN (2, 3)");
        guard[snapshotGuardIndex..].ShouldContain("\"ValueType\" IN (2, 3)");
        guard.ShouldContain("RAISE EXCEPTION");
        guard.ShouldContain(
            "Cannot downgrade while enum or string-set entitlements other than short-link statistics exist.");

        var foreignKey = operations.OfType<DropForeignKeyOperation>().Single(operation =>
            operation.Name == "FK_ShortLinkLinks_ShortLinkDomains_DomainId");
        var domainTable = operations.OfType<DropTableOperation>().Single(operation =>
            operation.Name == "ShortLinkDomains");
        operations.IndexOf(foreignKey).ShouldBeLessThan(operations.IndexOf(domainTable));

        var domainIndex = operations.OfType<DropIndexOperation>().Single(operation =>
            operation.Name == "IX_ShortLinkLinks_DomainId");
        var domainColumn = operations.OfType<DropColumnOperation>().Single(operation =>
            operation.Table == "ShortLinkLinks" && operation.Name == "DomainId");
        operations.IndexOf(domainIndex).ShouldBeLessThan(operations.IndexOf(domainColumn));

        var originIndex = operations.OfType<DropIndexOperation>().Single(operation =>
            operation.Name == "IX_ShortLinkLinks_Origin_Code");
        var originColumn = operations.OfType<DropColumnOperation>().Single(operation =>
            operation.Table == "ShortLinkLinks" && operation.Name == "Origin");
        operations.IndexOf(originIndex).ShouldBeLessThan(operations.IndexOf(originColumn));

        var legacyCodeIndex = operations.OfType<CreateIndexOperation>().Single();
        legacyCodeIndex.Name.ShouldBe("IX_ShortLinkLinks_Code");
        legacyCodeIndex.Columns.ShouldBe(["Code"]);
        legacyCodeIndex.IsUnique.ShouldBeTrue();
        legacyCodeIndex.Filter.ShouldBeNull();
        operations.IndexOf(originColumn).ShouldBeLessThan(operations.IndexOf(legacyCodeIndex));

        var restoredConstraints = operations.OfType<AddCheckConstraintOperation>().ToList();
        restoredConstraints.Select(operation => operation.Name).ShouldBe(
            ["CK_Subscription_Snapshot_Value", "CK_Subscription_PlanEntitlement_Value"],
            ignoreOrder: true);
        var lastValueColumnDrop = droppedValueColumns.Max(operation => operations.IndexOf(operation));
        restoredConstraints
            .All(operation => operations.IndexOf(operation) > lastValueColumnDrop)
            .ShouldBeTrue();
    }

    private static WebHostDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WebHostDbContext>().UseNpgsql().Options);
}
