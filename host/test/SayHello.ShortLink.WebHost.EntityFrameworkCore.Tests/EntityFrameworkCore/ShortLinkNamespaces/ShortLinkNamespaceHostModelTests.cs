using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SayHello.ShortLink.EntityFrameworkCore;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
using SayHello.ShortLink.WebHost.Migrations;
using Shouldly;
using Xunit;
using Link = SayHello.ShortLink.ShortLinks.ShortLink;

namespace SayHello.ShortLink.WebHost.EntityFrameworkCore.ShortLinkNamespaces;

public class ShortLinkNamespaceHostModelTests
{
    internal const string PreviousMigration = "20261010081017_AddShortLinkCustomCodeQuota";

    [Fact]
    public void Replacement_Context_Should_Expose_Namespace_And_Global_Reservation_DbSets()
    {
        using var context = CreateContext();
        var moduleContext = (IShortLinkDbContext)context;
        moduleContext.ShortLinkNamespaces.ShouldBeSameAs(context.ShortLinkNamespaces);
        moduleContext.ShortLinkNamespaceReservations.ShouldBeSameAs(context.ShortLinkNamespaceReservations);
        var model = context.GetService<IDesignTimeModel>().Model;
        var item = model.FindEntityType(typeof(ShortLinkNamespace))!;
        item.GetTableName().ShouldBe("ShortLinkNamespaces");
        item.FindProperty(nameof(ShortLinkNamespace.OwnerUserId))!.IsNullable.ShouldBeFalse();
        item.FindProperty(nameof(ShortLinkNamespace.ConcurrencyStamp))!.IsConcurrencyToken.ShouldBeTrue();
        item.GetForeignKeys().ShouldHaveSingleItem().PrincipalEntityType.ClrType.ShouldBe(typeof(ShortLinkDomain));
        var reservation = model.FindEntityType(typeof(ShortLinkNamespaceReservation))!;
        reservation.GetTableName().ShouldBe("ShortLinkNamespaceReservations");
        reservation.FindProperty("TenantId").ShouldBeNull();
        reservation.FindProperty("ConcurrencyStamp").ShouldBeNull();
        reservation.GetSeedData().ShouldHaveSingleItem()["Id"].ShouldBe("s");
    }

    [Fact]
    public void Link_Identity_Should_Use_Origin_Namespace_Code_And_Keep_Legacy_Root_Uniqueness()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var link = model.FindEntityType(typeof(Link))!;
        var path = link.FindProperty(nameof(Link.Namespace))!;
        path.IsNullable.ShouldBeFalse();
        path.GetMaxLength().ShouldBe(64);
        path.GetDefaultValue().ShouldBe(string.Empty);
        path.GetCollation().ShouldBe("C");
        link.FindProperty(nameof(Link.NamespaceId))!.IsNullable.ShouldBeTrue();
        var identity = link.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "Origin", "Namespace", "Code" }));
        identity.IsUnique.ShouldBeTrue();
        identity.GetFilter().ShouldBe("\"Origin\" IS NOT NULL");
        var legacy = link.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "Code" }));
        legacy.IsUnique.ShouldBeTrue();
        legacy.GetFilter().ShouldBe("\"Origin\" IS NULL");
        link.GetIndexes().ShouldNotContain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "Origin", "Code" }));
        var constraint = link.GetCheckConstraints().ShouldHaveSingleItem();
        constraint.Name.ShouldBe("CK_ShortLinkLinks_LegacyNamespace");
        constraint.Sql.ShouldBe("\"Origin\" IS NOT NULL OR (\"Namespace\" = '' AND \"NamespaceId\" IS NULL)");
        var namespaceReference = link.GetForeignKeys().Single(key =>
            key.PrincipalEntityType.ClrType == typeof(ShortLinkNamespace));
        namespaceReference.Properties.ShouldHaveSingleItem().Name.ShouldBe(nameof(Link.NamespaceId));
        namespaceReference.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
    }

    [Fact]
    public void Namespace_Identity_Should_Be_Case_Sensitive_Across_Owners_And_Tenants_With_Restrictive_Domain_Reference()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var item = model.FindEntityType(typeof(ShortLinkNamespace))!;
        var identity = item.GetIndexes().Single(index => index.IsUnique);
        identity.Properties.Select(property => property.Name).ShouldBe(["Origin", "Name"]);
        identity.GetFilter().ShouldBeNull();
        item.FindProperty(nameof(ShortLinkNamespace.Name))!.GetMaxLength().ShouldBe(64);
        item.FindProperty(nameof(ShortLinkNamespace.Name))!.GetCollation().ShouldBe("C");
        item.FindProperty(nameof(ShortLinkNamespace.Origin))!.GetCollation().ShouldBe("C");
        var reference = item.GetForeignKeys().ShouldHaveSingleItem();
        reference.Properties.ShouldHaveSingleItem().Name.ShouldBe(nameof(ShortLinkNamespace.DomainId));
        reference.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
        reference.IsRequired.ShouldBeTrue();
        model.FindEntityType(typeof(ShortLinkNamespaceReservation))!
            .FindProperty("Id")!.GetCollation().ShouldBe("C");
    }

    [Fact]
    public void Migration_Should_Backfill_Only_Empty_Root_Namespace_And_Add_The_Same_Constraints_And_Seed()
    {
        var migration = new AddShortLinkNamespaces();
        var operations = migration.UpOperations;
        operations.OfType<DropColumnOperation>().ShouldBeEmpty();
        operations.OfType<DropTableOperation>().ShouldBeEmpty();
        var dropped = operations.OfType<DropIndexOperation>().ShouldHaveSingleItem();
        dropped.Name.ShouldBe("IX_ShortLinkLinks_Origin_Code");
        var path = operations.OfType<AddColumnOperation>().Single(column => column.Name == "Namespace");
        path.Table.ShouldBe("ShortLinkLinks");
        path.IsNullable.ShouldBeFalse();
        path.MaxLength.ShouldBe(64);
        path.DefaultValue.ShouldBe(string.Empty);
        path.Collation.ShouldBe("C");
        operations.OfType<AddColumnOperation>().Single(column => column.Name == "NamespaceId")
            .IsNullable.ShouldBeTrue();
        operations.OfType<CreateTableOperation>().Select(table => table.Name)
            .ShouldBe(["ShortLinkNamespaceReservations", "ShortLinkNamespaces"]);
        var seed = operations.OfType<InsertDataOperation>().ShouldHaveSingleItem();
        seed.Table.ShouldBe("ShortLinkNamespaceReservations");
        seed.Columns.ShouldBe(["Id"]);
        seed.Values[0, 0].ShouldBe("s");
        var tuple = operations.OfType<CreateIndexOperation>().Single(index =>
            index.Name == "IX_ShortLinkLinks_Origin_Namespace_Code");
        tuple.Columns.ShouldBe(["Origin", "Namespace", "Code"]);
        tuple.IsUnique.ShouldBeTrue();
        tuple.Filter.ShouldBe("\"Origin\" IS NOT NULL");
        operations.OfType<AddCheckConstraintOperation>().ShouldHaveSingleItem()
            .Sql.ShouldContain("\"NamespaceId\" IS NULL");
        operations.OfType<AddForeignKeyOperation>().ShouldHaveSingleItem()
            .OnDelete.ShouldBe(ReferentialAction.Restrict);
        var namespaceTable = operations.OfType<CreateTableOperation>()
            .Single(table => table.Name == "ShortLinkNamespaces");
        namespaceTable.ForeignKeys.ShouldHaveSingleItem().OnDelete.ShouldBe(ReferentialAction.Restrict);
    }

    [Fact]
    public void Downgrade_Should_Guard_All_Identity_Data_Before_Any_Destructive_Operation()
    {
        var operations = new AddShortLinkNamespaces().DownOperations;
        var guard = operations.First().ShouldBeOfType<SqlOperation>();
        guard.SuppressTransaction.ShouldBeFalse();
        guard.Sql.ShouldContain("IN ACCESS EXCLUSIVE MODE");
        guard.Sql.ShouldContain("\"Namespace\" <> '' OR \"NamespaceId\" IS NOT NULL");
        guard.Sql.ShouldContain("IF EXISTS (SELECT 1 FROM \"ShortLinkNamespaces\")");
        guard.Sql.ShouldContain("\"ShortLinkNamespaceReservations\" WHERE \"Id\" <> 's'");
        guard.Sql.ShouldContain("Cannot downgrade while namespaced short links exist.");
        guard.Sql.ShouldContain("Cannot downgrade while user namespaces exist.");
        guard.Sql.ShouldContain("Cannot downgrade while historical namespace reservations other than s exist.");
        guard.Sql.ShouldNotContain("IsDeleted");
        guard.Sql.ShouldNotContain("TenantId");
        var restored = operations.OfType<CreateIndexOperation>().ShouldHaveSingleItem();
        restored.Columns.ShouldBe(["Origin", "Code"]);
        restored.IsUnique.ShouldBeTrue();
        restored.Filter.ShouldBe("\"Origin\" IS NOT NULL");
        operations.OfType<DropIndexOperation>().ShouldNotContain(index => index.Name == "IX_ShortLinkLinks_Code");
    }

    [Fact]
    public void Generated_PostgreSql_Scripts_And_Snapshot_Should_Match_The_Fresh_Model()
    {
        using var context = CreateContext();
        context.Database.HasPendingModelChanges().ShouldBeFalse();
        var migration = context.Database.GetMigrations().Single(name =>
            name.EndsWith("_AddShortLinkNamespaces", StringComparison.Ordinal));
        var migrator = context.GetService<IMigrator>();
        var upgrade = migrator.GenerateScript(PreviousMigration, migration);
        upgrade.ShouldContain("COLLATE \"C\"");
        upgrade.ShouldContain("DEFAULT ''");
        upgrade.ShouldContain("(\"Origin\", \"Namespace\", \"Code\") WHERE \"Origin\" IS NOT NULL");
        upgrade.ShouldContain("CK_ShortLinkLinks_LegacyNamespace");
        upgrade.ShouldContain("INSERT INTO \"ShortLinkNamespaceReservations\"");
        var downgrade = migrator.GenerateScript(migration, PreviousMigration);
        downgrade.IndexOf("RAISE EXCEPTION", StringComparison.Ordinal)
            .ShouldBeLessThan(downgrade.IndexOf("DROP TABLE", StringComparison.Ordinal));
        downgrade.ShouldContain("LOCK TABLE \"ShortLinkLinks\"");
        downgrade.ShouldContain("(\"Origin\", \"Code\") WHERE \"Origin\" IS NOT NULL");
    }

    private static WebHostDbContext CreateContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        WebHostEfCoreEntityExtensionMappings.Configure();
        return new WebHostDbContext(new DbContextOptionsBuilder<WebHostDbContext>().UseNpgsql().Options);
    }
}
