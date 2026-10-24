using Microsoft.EntityFrameworkCore;
using SayHello.ShortLink.BlockedDomains;
using SayHello.ShortLink.ShortLinkDomains;
using SayHello.ShortLink.ShortLinkNamespaces;
using SayHello.ShortLink.ShortLinks;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace SayHello.ShortLink.EntityFrameworkCore;

public static class ShortLinkDbContextModelCreatingExtensions
{
    public static void ConfigureShortLink(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        var namespaceCollation = builder.IsUsingPostgreSql()
            ? "C"
            : builder.IsUsingSqlite() ? "BINARY" : null;

        builder.Entity<ShortLinks.ShortLink>(b =>
        {
            b.ToTable(
                ShortLinkDbProperties.DbTablePrefix + "Links",
                ShortLinkDbProperties.DbSchema,
                table => table.HasCheckConstraint(
                    "CK_ShortLinkLinks_LegacyNamespace",
                    "\"Origin\" IS NOT NULL OR (\"Namespace\" = '' AND \"NamespaceId\" IS NULL)"));
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(ShortLinkConsts.MaxCodeLength);
            b.Property(x => x.Origin).HasMaxLength(ShortLinkDomainConsts.MaxOriginLength);
            b.Property(x => x.Namespace)
                .IsRequired()
                .HasMaxLength(ShortLinkNamespaceConsts.MaxNameLength)
                .HasDefaultValue(string.Empty)
                .UseCollation(namespaceCollation);
            b.Property(x => x.TargetUrl).IsRequired().HasMaxLength(ShortLinkConsts.MaxTargetUrlLength);
            b.Property(x => x.Title).HasMaxLength(ShortLinkConsts.MaxTitleLength);
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.IsCustomCode).IsRequired();
            b.Property(x => x.TotalVisitCount).IsRequired();
            b.HasIndex(x => new { x.Origin, x.Namespace, x.Code })
                .IsUnique()
                .HasFilter("\"Origin\" IS NOT NULL");
            b.HasIndex(x => x.Code)
                .IsUnique()
                .HasFilter("\"Origin\" IS NULL");
            b.HasOne<ShortLinkDomain>()
                .WithMany()
                .HasForeignKey(x => x.DomainId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ShortLinkNamespace>()
                .WithMany()
                .HasForeignKey(x => x.NamespaceId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.IsDeleted, x.CreationTime });
            b.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.IsDeleted, x.IsCustomCode });
            b.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.IsDeleted, x.Namespace });
            b.HasIndex(x => new { x.Status, x.ExpiresAt });
        });

        builder.Entity<ShortLinkVisit>(b =>
        {
            b.ToTable(ShortLinkDbProperties.DbTablePrefix + "Visits", ShortLinkDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.VisitorHash)
                .IsRequired()
                .HasMaxLength(ShortLinkConsts.VisitorHashLength)
                .IsFixedLength();
            b.Property(x => x.ReferrerHost).HasMaxLength(ShortLinkConsts.MaxHostLength);
            b.Property(x => x.Browser).IsRequired().HasMaxLength(ShortLinkConsts.MaxBrowserLength);
            b.Property(x => x.DeviceType).IsRequired().HasMaxLength(ShortLinkConsts.MaxDeviceTypeLength);
            b.HasOne<ShortLinks.ShortLink>()
                .WithMany()
                .HasForeignKey(x => x.ShortLinkId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ShortLinkId, x.VisitedAt });
            b.HasIndex(x => x.VisitedAt);
            b.HasIndex(x => new { x.ShortLinkId, x.VisitorHash, x.VisitedAt });
        });

        builder.Entity<ShortLinkDailyStatistic>(b =>
        {
            b.ToTable(
                ShortLinkDbProperties.DbTablePrefix + "DailyStatistics",
                ShortLinkDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.HasOne<ShortLinks.ShortLink>()
                .WithMany()
                .HasForeignKey(x => x.ShortLinkId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ShortLinkId, x.Date }).IsUnique();
            b.HasIndex(x => x.Date);
        });

        builder.Entity<ShortLinkDailyDimensionStatistic>(b =>
        {
            b.ToTable(
                ShortLinkDbProperties.DbTablePrefix + "DailyDimensionStatistics",
                ShortLinkDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Value)
                .IsRequired()
                .HasMaxLength(ShortLinkConsts.MaxDimensionValueLength);
            b.HasOne<ShortLinks.ShortLink>()
                .WithMany()
                .HasForeignKey(x => x.ShortLinkId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ShortLinkId, x.Date, x.Dimension, x.Value }).IsUnique();
            b.HasIndex(x => new { x.Dimension, x.Date });
        });

        builder.Entity<BlockedDomain>(b =>
        {
            b.ToTable(ShortLinkDbProperties.DbTablePrefix + "BlockedDomains", ShortLinkDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Domain).IsRequired().HasMaxLength(ShortLinkConsts.MaxHostLength);
            b.Property(x => x.Reason).HasMaxLength(BlockedDomainConsts.MaxReasonLength);
            // Unique per tenant (host rows have a NULL TenantId) and ignoring soft-deleted rows.
            b.HasIndex(x => new { x.TenantId, x.Domain })
                .IsUnique()
                .HasFilter("\"TenantId\" IS NOT NULL AND NOT \"IsDeleted\"");
            b.HasIndex(x => x.Domain)
                .IsUnique()
                .HasFilter("\"TenantId\" IS NULL AND NOT \"IsDeleted\"");
            b.HasIndex(x => new { x.IsActive, x.Domain });
        });

        builder.Entity<ShortLinkDomain>(b =>
        {
            b.ToTable(
                ShortLinkDbProperties.DbTablePrefix + "Domains",
                ShortLinkDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Origin)
                .IsRequired()
                .HasMaxLength(ShortLinkDomainConsts.MaxOriginLength);
            b.Property(x => x.IsEnabled).IsRequired();
            b.Property(x => x.IsDefault).IsRequired();
            b.Property(x => x.TenantScopeKey)
                .IsRequired()
                .HasMaxLength(ShortLinkDomainConsts.MaxTenantScopeKeyLength);
            b.Property(x => x.DefaultUniquenessKey)
                .IsRequired()
                .HasMaxLength(ShortLinkDomainConsts.MaxDefaultUniquenessKeyLength);
            b.HasIndex(x => new { x.TenantScopeKey, x.Origin }).IsUnique();
            b.HasIndex(x => x.DefaultUniquenessKey).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.IsEnabled, x.Origin });
        });

        builder.Entity<ShortLinkNamespace>(b =>
        {
            b.ToTable(
                ShortLinkDbProperties.DbTablePrefix + "Namespaces",
                ShortLinkDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Origin)
                .IsRequired()
                .HasMaxLength(ShortLinkDomainConsts.MaxOriginLength)
                .UseCollation(namespaceCollation);
            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(ShortLinkNamespaceConsts.MaxNameLength)
                .UseCollation(namespaceCollation);
            b.HasIndex(x => new { x.Origin, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.Origin });
            b.HasOne<ShortLinkDomain>()
                .WithMany()
                .HasForeignKey(x => x.DomainId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ShortLinkNamespaceReservation>(b =>
        {
            b.ToTable(
                ShortLinkDbProperties.DbTablePrefix + "NamespaceReservations",
                ShortLinkDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Id)
                .IsRequired()
                .HasMaxLength(ShortLinkNamespaceConsts.MaxNameLength)
                .UseCollation(namespaceCollation);
            b.HasData(new ShortLinkNamespaceReservation(ShortLinkNamespaceConsts.DefaultNamespace));
        });
    }
}
