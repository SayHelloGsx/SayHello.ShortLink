using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SayHello.ShortLink.WebHost.Migrations
{
    /// <inheritdoc />
    public partial class AddShortLinkNamespaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_Origin_Code",
                table: "ShortLinkLinks");

            migrationBuilder.AddColumn<string>(
                name: "Namespace",
                table: "ShortLinkLinks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                collation: "C");

            migrationBuilder.AddColumn<Guid>(
                name: "NamespaceId",
                table: "ShortLinkLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShortLinkNamespaceReservations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortLinkNamespaceReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShortLinkNamespaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DomainId = table.Column<Guid>(type: "uuid", nullable: false),
                    Origin = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortLinkNamespaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShortLinkNamespaces_ShortLinkDomains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "ShortLinkDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ShortLinkNamespaceReservations",
                column: "Id",
                value: "s");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_NamespaceId",
                table: "ShortLinkLinks",
                column: "NamespaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_Origin_Namespace_Code",
                table: "ShortLinkLinks",
                columns: new[] { "Origin", "Namespace", "Code" },
                unique: true,
                filter: "\"Origin\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_TenantId_OwnerUserId_IsDeleted_Namespace",
                table: "ShortLinkLinks",
                columns: new[] { "TenantId", "OwnerUserId", "IsDeleted", "Namespace" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShortLinkLinks_LegacyNamespace",
                table: "ShortLinkLinks",
                sql: "\"Origin\" IS NOT NULL OR (\"Namespace\" = '' AND \"NamespaceId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkNamespaces_DomainId",
                table: "ShortLinkNamespaces",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkNamespaces_Origin_Name",
                table: "ShortLinkNamespaces",
                columns: new[] { "Origin", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkNamespaces_TenantId_OwnerUserId_Origin",
                table: "ShortLinkNamespaces",
                columns: new[] { "TenantId", "OwnerUserId", "Origin" });

            migrationBuilder.AddForeignKey(
                name: "FK_ShortLinkLinks_ShortLinkNamespaces_NamespaceId",
                table: "ShortLinkLinks",
                column: "NamespaceId",
                principalTable: "ShortLinkNamespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE "ShortLinkLinks", "ShortLinkNamespaces", "ShortLinkNamespaceReservations"
                    IN ACCESS EXCLUSIVE MODE;

                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "ShortLinkLinks"
                        WHERE "Namespace" <> '' OR "NamespaceId" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade while namespaced short links exist.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM "ShortLinkNamespaces") THEN
                        RAISE EXCEPTION 'Cannot downgrade while user namespaces exist.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM "ShortLinkNamespaceReservations" WHERE "Id" <> 's'
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade while historical namespace reservations other than s exist.';
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ShortLinkLinks_ShortLinkNamespaces_NamespaceId",
                table: "ShortLinkLinks");

            migrationBuilder.DropTable(
                name: "ShortLinkNamespaceReservations");

            migrationBuilder.DropTable(
                name: "ShortLinkNamespaces");

            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_NamespaceId",
                table: "ShortLinkLinks");

            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_Origin_Namespace_Code",
                table: "ShortLinkLinks");

            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_TenantId_OwnerUserId_IsDeleted_Namespace",
                table: "ShortLinkLinks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShortLinkLinks_LegacyNamespace",
                table: "ShortLinkLinks");

            migrationBuilder.DropColumn(
                name: "Namespace",
                table: "ShortLinkLinks");

            migrationBuilder.DropColumn(
                name: "NamespaceId",
                table: "ShortLinkLinks");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_Origin_Code",
                table: "ShortLinkLinks",
                columns: new[] { "Origin", "Code" },
                unique: true,
                filter: "\"Origin\" IS NOT NULL");
        }
    }
}
