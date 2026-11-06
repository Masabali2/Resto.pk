using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantSaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class t40 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Carts_TenantId_SessionId",
                table: "Carts");

            migrationBuilder.CreateIndex(
                name: "UX_Carts_TenantId_RestaurantId_SessionId_Active",
                table: "Carts",
                columns: new[] { "TenantId", "RestaurantId", "SessionId" },
                unique: true,
                filter: "[SessionId] IS NOT NULL AND [Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Carts_TenantId_RestaurantId_SessionId_Active",
                table: "Carts");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_TenantId_SessionId",
                table: "Carts",
                columns: new[] { "TenantId", "SessionId" },
                filter: "[SessionId] IS NOT NULL");
        }
    }
}
