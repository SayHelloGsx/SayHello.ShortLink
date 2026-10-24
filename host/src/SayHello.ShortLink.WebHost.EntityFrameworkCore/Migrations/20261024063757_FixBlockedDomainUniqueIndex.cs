using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SayHello.ShortLink.WebHost.Migrations
{
    /// <inheritdoc />
    public partial class FixBlockedDomainUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShortLinkBlockedDomains_Domain",
                table: "ShortLinkBlockedDomains");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkBlockedDomains_Domain",
                table: "ShortLinkBlockedDomains",
                column: "Domain",
                unique: true,
                filter: "\"TenantId\" IS NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkBlockedDomains_TenantId_Domain",
                table: "ShortLinkBlockedDomains",
                columns: new[] { "TenantId", "Domain" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShortLinkBlockedDomains_Domain",
                table: "ShortLinkBlockedDomains");

            migrationBuilder.DropIndex(
                name: "IX_ShortLinkBlockedDomains_TenantId_Domain",
                table: "ShortLinkBlockedDomains");

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkBlockedDomains_Domain",
                table: "ShortLinkBlockedDomains",
                column: "Domain",
                unique: true);
        }
    }
}
