using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SayHello.Subscription.Catalog;
using SayHello.Subscription.Subscriptions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace SayHello.Subscription.EntityFrameworkCore;

public static class SubscriptionDbContextModelCreatingExtensions
{
    private const string ValueConstraint =
        "(\"ValueType\" = 0 AND \"BooleanValue\" IS NOT NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE) OR " +
        "(\"ValueType\" = 1 AND \"BooleanValue\" IS NULL AND ((\"IsUnlimited\" = TRUE AND \"NumericValue\" IS NULL) OR " +
        "(\"IsUnlimited\" = FALSE AND \"NumericValue\" IS NOT NULL AND \"NumericValue\" >= 0)))";

    // Stored lifecycle values are UTC even when a provider returns timestamps without a DateTime kind.
    private static readonly ValueConverter<DateTime, DateTime> UtcTimestampConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static void ConfigureSubscription(this ModelBuilder builder)
    {
        var prefix = SubscriptionDbProperties.DbTablePrefix;
        var schema = SubscriptionDbProperties.DbSchema;

        builder.Entity<SubscriptionProduct>(b =>
        {
            b.ToTable(prefix + "Products", schema);
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(SubscriptionConsts.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(SubscriptionConsts.MaxNameLength);
            b.Property(x => x.Description).HasMaxLength(SubscriptionConsts.MaxDescriptionLength);
            b.HasIndex(x => x.Code, "UX_Subscription_Product_HostCode").IsUnique().HasFilter("\"TenantId\" IS NULL");
            b.HasIndex(x => new { x.TenantId, x.Code }, "UX_Subscription_Product_TenantCode")
                .IsUnique().HasFilter("\"TenantId\" IS NOT NULL");
        });
        builder.Entity<SubscriptionPlan>(b =>
        {
            b.ToTable(prefix + "Plans", schema);
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(SubscriptionConsts.MaxCodeLength);
            b.Property(x => x.ProductCode).IsRequired().HasMaxLength(SubscriptionConsts.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(SubscriptionConsts.MaxNameLength);
            b.Property(x => x.Description).HasMaxLength(SubscriptionConsts.MaxDescriptionLength);
            b.HasIndex(x => new { x.ProductId, x.Code }, "UX_Subscription_Plan_Code").IsUnique();
            b.HasAlternateKey(x => new { x.Id, x.ProductId });
            b.HasOne<SubscriptionProduct>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Entitlements).WithOne().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Entitlements).HasField("_entitlements").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<SubscriptionPlanEntitlement>(b =>
        {
            b.ToTable(prefix + "PlanEntitlements", schema,
                table => table.HasCheckConstraint("CK_Subscription_PlanEntitlement_Value", ValueConstraint));
            b.ConfigureByConvention();
            b.HasKey(x => new { x.PlanId, x.FeatureKey });
            b.Property(x => x.FeatureKey).IsRequired().HasMaxLength(SubscriptionConsts.MaxFeatureKeyLength);
        });
        builder.Entity<SubscriptionBundle>(b =>
        {
            b.ToTable(prefix + "Bundles", schema);
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(SubscriptionConsts.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(SubscriptionConsts.MaxNameLength);
            b.Property(x => x.Description).HasMaxLength(SubscriptionConsts.MaxDescriptionLength);
            b.HasIndex(x => x.Code, "UX_Subscription_Bundle_HostCode").IsUnique().HasFilter("\"TenantId\" IS NULL");
            b.HasIndex(x => new { x.TenantId, x.Code }, "UX_Subscription_Bundle_TenantCode")
                .IsUnique().HasFilter("\"TenantId\" IS NOT NULL");
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.BundleId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<SubscriptionBundleItem>(b =>
        {
            b.ToTable(prefix + "BundleItems", schema);
            b.ConfigureByConvention();
            b.HasKey(x => new { x.BundleId, x.ProductId });
            b.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => new { x.PlanId, x.ProductId })
                .HasPrincipalKey(x => new { x.Id, x.ProductId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SubscriptionProduct>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<UserSubscription>(b =>
        {
            b.ToTable(prefix + "UserSubscriptions", schema, table =>
            {
                table.HasCheckConstraint("CK_Subscription_CurrentState",
                    "(\"IsCurrent\" = TRUE AND \"EndedAt\" IS NULL AND \"EndReason\" IS NULL) OR " +
                    "(\"IsCurrent\" = FALSE AND \"EndedAt\" IS NOT NULL AND \"EndReason\" IS NOT NULL)");
                table.HasCheckConstraint("CK_Subscription_Expiration", "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"StartsAt\"");
            });
            b.ConfigureByConvention();
            b.Property(x => x.StartsAt).HasConversion(UtcTimestampConverter);
            b.Property(x => x.ExpiresAt).HasConversion(UtcTimestampConverter);
            b.Property(x => x.EndedAt).HasConversion(UtcTimestampConverter);
            b.Property(x => x.ProductCode).IsRequired().HasMaxLength(SubscriptionConsts.MaxCodeLength);
            b.Property(x => x.PlanCode).IsRequired().HasMaxLength(SubscriptionConsts.MaxCodeLength);
            b.Property(x => x.BundleCode).HasMaxLength(SubscriptionConsts.MaxCodeLength);
            b.Property(x => x.ProductName).IsRequired().HasMaxLength(SubscriptionConsts.MaxNameLength);
            b.Property(x => x.PlanName).IsRequired().HasMaxLength(SubscriptionConsts.MaxNameLength);
            b.Property(x => x.BundleName).HasMaxLength(SubscriptionConsts.MaxNameLength);
            b.Property(x => x.EndReasonDetail).HasMaxLength(SubscriptionConsts.MaxReasonLength);
            b.HasIndex(x => new { x.UserId, x.ProductId }, "UX_Subscription_Current_Host")
                .IsUnique().HasFilter("\"TenantId\" IS NULL AND \"IsCurrent\" = TRUE");
            b.HasIndex(x => new { x.TenantId, x.UserId, x.ProductId }, "UX_Subscription_Current_Tenant")
                .IsUnique().HasFilter("\"TenantId\" IS NOT NULL AND \"IsCurrent\" = TRUE");
            b.HasIndex(x => new { x.TenantId, x.UserId, x.StartsAt });
            b.HasOne<SubscriptionProduct>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => new { x.SourcePlanId, x.ProductId })
                .HasPrincipalKey(x => new { x.Id, x.ProductId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SubscriptionBundle>().WithMany().HasForeignKey(x => x.SourceBundleId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Entitlements).WithOne().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Entitlements).HasField("_entitlements").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<UserSubscriptionEntitlement>(b =>
        {
            b.ToTable(prefix + "UserSubscriptionEntitlements", schema,
                table => table.HasCheckConstraint("CK_Subscription_Snapshot_Value", ValueConstraint));
            b.ConfigureByConvention();
            b.HasKey(x => new { x.SubscriptionId, x.FeatureKey });
            b.Property(x => x.FeatureKey).IsRequired().HasMaxLength(SubscriptionConsts.MaxFeatureKeyLength);
            b.Property(x => x.DisplayName).IsRequired().HasMaxLength(SubscriptionConsts.MaxNameLength);
        });
    }
}
