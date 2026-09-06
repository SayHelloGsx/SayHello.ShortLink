using System.Linq;
using Microsoft.EntityFrameworkCore;
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

    private static WebHostDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WebHostDbContext>().UseNpgsql().Options);
}
