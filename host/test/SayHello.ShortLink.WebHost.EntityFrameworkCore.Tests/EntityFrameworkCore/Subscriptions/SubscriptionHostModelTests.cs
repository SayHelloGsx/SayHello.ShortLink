using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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

    private static WebHostDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WebHostDbContext>().UseNpgsql().Options);
}
