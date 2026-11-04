using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantSaaS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class m32_website_template_design_configuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DesignConfigurationJson",
                table: "WebsiteTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WebsiteTemplates_ValidDesignJson",
                table: "WebsiteTemplates",
                sql: "[DesignConfigurationJson] IS NULL OR ISJSON([DesignConfigurationJson]) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WebsiteTemplates_ValidDesignJson",
                table: "WebsiteTemplates");

            migrationBuilder.DropColumn(
                name: "DesignConfigurationJson",
                table: "WebsiteTemplates");
        }
    }
}
