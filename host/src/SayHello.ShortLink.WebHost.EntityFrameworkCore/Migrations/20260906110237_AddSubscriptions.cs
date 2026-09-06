using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SayHello.ShortLink.WebHost.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionBundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionBundles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.UniqueConstraint("AK_SubscriptionPlans_Id_ProductId", x => new { x.Id, x.ProductId });
                    table.ForeignKey(
                        name: "FK_SubscriptionPlans_SubscriptionProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "SubscriptionProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionBundleItems",
                columns: table => new
                {
                    BundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionBundleItems", x => new { x.BundleId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_SubscriptionBundleItems_SubscriptionBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "SubscriptionBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriptionBundleItems_SubscriptionPlans_PlanId_ProductId",
                        columns: x => new { x.PlanId, x.ProductId },
                        principalTable: "SubscriptionPlans",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionBundleItems_SubscriptionProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "SubscriptionProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanEntitlements",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValueType = table.Column<int>(type: "integer", nullable: false),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    NumericValue = table.Column<long>(type: "bigint", nullable: true),
                    IsUnlimited = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanEntitlements", x => new { x.PlanId, x.FeatureKey });
                    table.CheckConstraint("CK_Subscription_PlanEntitlement_Value", "(\"ValueType\" = 0 AND \"BooleanValue\" IS NOT NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE) OR (\"ValueType\" = 1 AND \"BooleanValue\" IS NULL AND ((\"IsUnlimited\" = TRUE AND \"NumericValue\" IS NULL) OR (\"IsUnlimited\" = FALSE AND \"NumericValue\" IS NOT NULL AND \"NumericValue\" >= 0)))");
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanEntitlements_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionUserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBundleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlanName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BundleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BundleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndReason = table.Column<int>(type: "integer", nullable: true),
                    EndReasonDetail = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionUserSubscriptions", x => x.Id);
                    table.CheckConstraint("CK_Subscription_CurrentState", "(\"IsCurrent\" = TRUE AND \"EndedAt\" IS NULL AND \"EndReason\" IS NULL) OR (\"IsCurrent\" = FALSE AND \"EndedAt\" IS NOT NULL AND \"EndReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_Subscription_Expiration", "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_SubscriptionUserSubscriptions_SubscriptionBundles_SourceBun~",
                        column: x => x.SourceBundleId,
                        principalTable: "SubscriptionBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionUserSubscriptions_SubscriptionPlans_SourcePlanI~",
                        columns: x => new { x.SourcePlanId, x.ProductId },
                        principalTable: "SubscriptionPlans",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionUserSubscriptions_SubscriptionProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "SubscriptionProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionUserSubscriptionEntitlements",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ValueType = table.Column<int>(type: "integer", nullable: false),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    NumericValue = table.Column<long>(type: "bigint", nullable: true),
                    IsUnlimited = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionUserSubscriptionEntitlements", x => new { x.SubscriptionId, x.FeatureKey });
                    table.CheckConstraint("CK_Subscription_Snapshot_Value", "(\"ValueType\" = 0 AND \"BooleanValue\" IS NOT NULL AND \"NumericValue\" IS NULL AND \"IsUnlimited\" = FALSE) OR (\"ValueType\" = 1 AND \"BooleanValue\" IS NULL AND ((\"IsUnlimited\" = TRUE AND \"NumericValue\" IS NULL) OR (\"IsUnlimited\" = FALSE AND \"NumericValue\" IS NOT NULL AND \"NumericValue\" >= 0)))");
                    table.ForeignKey(
                        name: "FK_SubscriptionUserSubscriptionEntitlements_SubscriptionUserSu~",
                        column: x => x.SubscriptionId,
                        principalTable: "SubscriptionUserSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBundleItems_PlanId_ProductId",
                table: "SubscriptionBundleItems",
                columns: new[] { "PlanId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionBundleItems_ProductId",
                table: "SubscriptionBundleItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_Subscription_Bundle_HostCode",
                table: "SubscriptionBundles",
                column: "Code",
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Subscription_Bundle_TenantCode",
                table: "SubscriptionBundles",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Subscription_Plan_Code",
                table: "SubscriptionPlans",
                columns: new[] { "ProductId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Subscription_Product_HostCode",
                table: "SubscriptionProducts",
                column: "Code",
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Subscription_Product_TenantCode",
                table: "SubscriptionProducts",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUserSubscriptions_ProductId",
                table: "SubscriptionUserSubscriptions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUserSubscriptions_SourceBundleId",
                table: "SubscriptionUserSubscriptions",
                column: "SourceBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUserSubscriptions_SourcePlanId_ProductId",
                table: "SubscriptionUserSubscriptions",
                columns: new[] { "SourcePlanId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUserSubscriptions_TenantId_UserId_StartsAt",
                table: "SubscriptionUserSubscriptions",
                columns: new[] { "TenantId", "UserId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Subscription_Current_Host",
                table: "SubscriptionUserSubscriptions",
                columns: new[] { "UserId", "ProductId" },
                unique: true,
                filter: "\"TenantId\" IS NULL AND \"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "UX_Subscription_Current_Tenant",
                table: "SubscriptionUserSubscriptions",
                columns: new[] { "TenantId", "UserId", "ProductId" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL AND \"IsCurrent\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionBundleItems");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanEntitlements");

            migrationBuilder.DropTable(
                name: "SubscriptionUserSubscriptionEntitlements");

            migrationBuilder.DropTable(
                name: "SubscriptionUserSubscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionBundles");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "SubscriptionProducts");
        }
    }
}
