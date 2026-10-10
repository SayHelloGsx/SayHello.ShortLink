using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SayHello.ShortLink.WebHost.Migrations
{
    /// <inheritdoc />
    public partial class AddShortLinkCustomCodeQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCustomCode",
                table: "ShortLinkLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ShortLinkLinks_TenantId_OwnerUserId_IsDeleted_IsCustomCode",
                table: "ShortLinkLinks",
                columns: new[] { "TenantId", "OwnerUserId", "IsDeleted", "IsCustomCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShortLinkLinks_TenantId_OwnerUserId_IsDeleted_IsCustomCode",
                table: "ShortLinkLinks");

            migrationBuilder.DropColumn(
                name: "IsCustomCode",
                table: "ShortLinkLinks");
        }
    }
}
