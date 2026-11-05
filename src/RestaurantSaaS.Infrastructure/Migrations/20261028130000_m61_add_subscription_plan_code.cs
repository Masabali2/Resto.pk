using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantSaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class m61_add_subscription_plan_code : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "SubscriptionPlans",
                type: "nvarchar(100)",
                nullable: true);

            // Backfill existing rows so the column can be made required. The
            // derived code matches the code the seeder assigns to the free
            // trial plan ("free-trial").
            migrationBuilder.Sql(
                "UPDATE [SubscriptionPlans] SET [Code] = LOWER(REPLACE([Name], ' ', '-')) WHERE [Code] IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "SubscriptionPlans",
                type: "nvarchar(100)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_SubscriptionPlans_Code_BillingInterval",
                table: "SubscriptionPlans",
                columns: new[] { "Code", "BillingInterval" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_SubscriptionPlans_Code_BillingInterval",
                table: "SubscriptionPlans");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "SubscriptionPlans",
                type: "nvarchar(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldNullable: false);

            migrationBuilder.DropColumn(
                name: "Code",
                table: "SubscriptionPlans");
        }
    }
}
