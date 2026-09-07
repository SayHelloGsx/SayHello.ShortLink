using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SayHello.ShortLink.WebHost.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionDefaultPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPlanId",
                table: "SubscriptionProducts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionProducts_DefaultPlanId_Id",
                table: "SubscriptionProducts",
                columns: new[] { "DefaultPlanId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionProducts_SubscriptionPlans_DefaultPlanId_Id",
                table: "SubscriptionProducts",
                columns: new[] { "DefaultPlanId", "Id" },
                principalTable: "SubscriptionPlans",
                principalColumns: new[] { "Id", "ProductId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionProducts_SubscriptionPlans_DefaultPlanId_Id",
                table: "SubscriptionProducts");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionProducts_DefaultPlanId_Id",
                table: "SubscriptionProducts");

            migrationBuilder.DropColumn(
                name: "DefaultPlanId",
                table: "SubscriptionProducts");
        }
    }
}
