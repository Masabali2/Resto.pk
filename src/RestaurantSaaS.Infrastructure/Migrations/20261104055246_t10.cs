using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantSaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class t10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DesignConfigurationJson",
                table: "WebsiteTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "SubscriptionPlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "Files",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Files",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDedupeKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDedupeKeys", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WebsiteTemplates_ValidDesignJson",
                table: "WebsiteTemplates",
                sql: "[DesignConfigurationJson] IS NULL OR ISJSON([DesignConfigurationJson]) = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_TenantId_Live",
                table: "Subscriptions",
                column: "TenantId",
                unique: true,
                filter: "[Status] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "UX_SubscriptionPlans_Code_BillingInterval",
                table: "SubscriptionPlans",
                columns: new[] { "Code", "BillingInterval" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_Status_NextAttemptAtUtc",
                table: "EmailOutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_EmailOutboxMessages_EventName_DedupeKey",
                table: "EmailOutboxMessages",
                columns: new[] { "EventName", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_NotificationDedupeKeys_EventKey",
                table: "NotificationDedupeKeys",
                column: "EventKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailOutboxMessages");

            migrationBuilder.DropTable(
                name: "NotificationDedupeKeys");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WebsiteTemplates_ValidDesignJson",
                table: "WebsiteTemplates");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_TenantId_Live",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_SubscriptionPlans_Code_BillingInterval",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "DesignConfigurationJson",
                table: "WebsiteTemplates");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Files");
        }
    }
}
