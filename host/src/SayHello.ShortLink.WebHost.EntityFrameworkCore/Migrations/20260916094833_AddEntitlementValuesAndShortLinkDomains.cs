using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SayHello.ShortLink.WebHost.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitlementValuesAndShortLinkDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscription_Snapshot_Value",
                table: "SubscriptionUserSubscriptionEntitlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscription_PlanEntitlement_Value",
                table: "SubscriptionPlanEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_Code",
                table: "ShortLinkLinks");

            migrationBuilder.AddColumn<string>(
                name: "StringSetValue",
                table: "SubscriptionUserSubscriptionEntitlements",
                type: "character varying(307501)",
                maxLength: 307501,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StringValue",
                table: "SubscriptionUserSubscriptionEntitlements",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StringSetValue",
                table: "SubscriptionPlanEntitlements",
                type: "character varying(307501)",
                maxLength: 307501,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StringValue",
                table: "SubscriptionPlanEntitlements",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DomainId",
                table: "ShortLinkLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "ShortLinkLinks",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "SubscriptionPlanEntitlements" AS entitlement
                SET "ValueType" = 2,
                    "StringValue" = CASE
                        WHEN entitlement."BooleanValue" = TRUE THEN 'advanced'
                        ELSE 'basic'
                    END,
                    "BooleanValue" = NULL
                FROM "SubscriptionPlans" AS plan
                WHERE entitlement."PlanId" = plan."Id"
                  AND plan."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'statistics'
                  AND entitlement."ValueType" = 0
                  AND entitlement."BooleanValue" IS NOT NULL;

                UPDATE "SubscriptionUserSubscriptionEntitlements" AS entitlement
                SET "ValueType" = 2,
                    "StringValue" = CASE
                        WHEN entitlement."BooleanValue" = TRUE THEN 'advanced'
                        ELSE 'basic'
                    END,
                    "BooleanValue" = NULL
                FROM "SubscriptionUserSubscriptions" AS subscription
                WHERE entitlement."SubscriptionId" = subscription."Id"
                  AND subscription."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'statistics'
                  AND entitlement."ValueType" = 0
                  AND entitlement."BooleanValue" IS NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "ShortLinkDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    TenantScopeKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultUniquenessKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortLinkDomains", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscription_Snapshot_Value",
                table: "SubscriptionUserSubscriptionEntitlements",
                sql: "(\"ValueType\" = 0 AND \"BooleanValue\" IS NOT NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE AND \"StringValue\" IS NULL AND \"StringSetValue\" IS NULL) OR (\"ValueType\" = 1 AND \"BooleanValue\" IS NULL AND \"StringValue\" IS NULL AND \"StringSetValue\" IS NULL AND ((\"IsUnlimited\" = TRUE AND \"NumericValue\" IS NULL) OR (\"IsUnlimited\" = FALSE AND \"NumericValue\" IS NOT NULL AND \"NumericValue\" >= 0))) OR (\"ValueType\" = 2 AND \"BooleanValue\" IS NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE AND \"StringValue\" IS NOT NULL AND \"StringSetValue\" IS NULL) OR (\"ValueType\" = 3 AND \"BooleanValue\" IS NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE AND \"StringValue\" IS NULL AND \"StringSetValue\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscription_PlanEntitlement_Value",
                table: "SubscriptionPlanEntitlements",
                sql: "(\"ValueType\" = 0 AND \"BooleanValue\" IS NOT NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE AND \"StringValue\" IS NULL AND \"StringSetValue\" IS NULL) OR (\"ValueType\" = 1 AND \"BooleanValue\" IS NULL AND \"StringValue\" IS NULL AND \"StringSetValue\" IS NULL AND ((\"IsUnlimited\" = TRUE AND \"NumericValue\" IS NULL) OR (\"IsUnlimited\" = FALSE AND \"NumericValue\" IS NOT NULL AND \"NumericValue\" >= 0))) OR (\"ValueType\" = 2 AND \"BooleanValue\" IS NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE AND \"StringValue\" IS NOT NULL AND \"StringSetValue\" IS NULL) OR (\"ValueType\" = 3 AND \"BooleanValue\" IS NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE AND \"StringValue\" IS NULL AND \"StringSetValue\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_Code",
                table: "ShortLinkLinks",
                column: "Code",
                unique: true,
                filter: "\"Origin\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_DomainId",
                table: "ShortLinkLinks",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_Origin_Code",
                table: "ShortLinkLinks",
                columns: new[] { "Origin", "Code" },
                unique: true,
                filter: "\"Origin\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkDomains_DefaultUniquenessKey",
                table: "ShortLinkDomains",
                column: "DefaultUniquenessKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkDomains_TenantId_IsEnabled_Origin",
                table: "ShortLinkDomains",
                columns: new[] { "TenantId", "IsEnabled", "Origin" });

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkDomains_TenantScopeKey_Origin",
                table: "ShortLinkDomains",
                columns: new[] { "TenantScopeKey", "Origin" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShortLinkLinks_ShortLinkDomains_DomainId",
                table: "ShortLinkLinks",
                column: "DomainId",
                principalTable: "ShortLinkDomains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShortLinkLinks_ShortLinkDomains_DomainId",
                table: "ShortLinkLinks");

            migrationBuilder.DropTable(
                name: "ShortLinkDomains");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscription_Snapshot_Value",
                table: "SubscriptionUserSubscriptionEntitlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscription_PlanEntitlement_Value",
                table: "SubscriptionPlanEntitlements");

            migrationBuilder.Sql(
                """
                UPDATE "SubscriptionPlanEntitlements" AS entitlement
                SET "ValueType" = 0,
                    "BooleanValue" = entitlement."StringValue" = 'advanced',
                    "StringValue" = NULL
                FROM "SubscriptionPlans" AS plan
                WHERE entitlement."PlanId" = plan."Id"
                  AND plan."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'statistics'
                  AND entitlement."ValueType" = 2
                  AND entitlement."StringValue" IN ('none', 'basic', 'advanced');

                UPDATE "SubscriptionUserSubscriptionEntitlements" AS entitlement
                SET "ValueType" = 0,
                    "BooleanValue" = entitlement."StringValue" = 'advanced',
                    "StringValue" = NULL
                FROM "SubscriptionUserSubscriptions" AS subscription
                WHERE entitlement."SubscriptionId" = subscription."Id"
                  AND subscription."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'statistics'
                  AND entitlement."ValueType" = 2
                  AND entitlement."StringValue" IN ('none', 'basic', 'advanced');

                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "SubscriptionPlanEntitlements"
                        WHERE "ValueType" IN (2, 3)
                    ) OR EXISTS (
                        SELECT 1
                        FROM "SubscriptionUserSubscriptionEntitlements"
                        WHERE "ValueType" IN (2, 3)
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while enum or string-set entitlements other than short-link statistics exist.';
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_Code",
                table: "ShortLinkLinks");

            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_DomainId",
                table: "ShortLinkLinks");

            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_Origin_Code",
                table: "ShortLinkLinks");

            migrationBuilder.DropColumn(
                name: "StringSetValue",
                table: "SubscriptionUserSubscriptionEntitlements");

            migrationBuilder.DropColumn(
                name: "StringValue",
                table: "SubscriptionUserSubscriptionEntitlements");

            migrationBuilder.DropColumn(
                name: "StringSetValue",
                table: "SubscriptionPlanEntitlements");

            migrationBuilder.DropColumn(
                name: "StringValue",
                table: "SubscriptionPlanEntitlements");

            migrationBuilder.DropColumn(
                name: "DomainId",
                table: "ShortLinkLinks");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "ShortLinkLinks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscription_Snapshot_Value",
                table: "SubscriptionUserSubscriptionEntitlements",
                sql: "(\"ValueType\" = 0 AND \"BooleanValue\" IS NOT NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE) OR (\"ValueType\" = 1 AND \"BooleanValue\" IS NULL AND ((\"IsUnlimited\" = TRUE AND \"NumericValue\" IS NULL) OR (\"IsUnlimited\" = FALSE AND \"NumericValue\" IS NOT NULL AND \"NumericValue\" >= 0)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscription_PlanEntitlement_Value",
                table: "SubscriptionPlanEntitlements",
                sql: "(\"ValueType\" = 0 AND \"BooleanValue\" IS NOT NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE) OR (\"ValueType\" = 1 AND \"BooleanValue\" IS NULL AND ((\"IsUnlimited\" = TRUE AND \"NumericValue\" IS NULL) OR (\"IsUnlimited\" = FALSE AND \"NumericValue\" IS NOT NULL AND \"NumericValue\" >= 0)))");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_Code",
                table: "ShortLinkLinks",
                column: "Code",
                unique: true);
        }
    }
}
