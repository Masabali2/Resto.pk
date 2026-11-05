using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantSaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTrackingAndCartConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingTokenHash",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Carts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TrackingTokenHash",
                table: "Orders",
                column: "TrackingTokenHash",
                unique: true,
                filter: "[TrackingTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_TrackingTokenHash",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrackingTokenHash",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Carts");
        }
    }
}
