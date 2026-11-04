using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantSaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class m31_subscription_live_guard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_TenantId_Live",
                table: "Subscriptions",
                column: "TenantId",
                unique: true,
                filter: "[Status] IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_TenantId_Live",
                table: "Subscriptions");
        }
    }
}
