using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketingAutomation.Modules.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantSendingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxMarketingPerDay",
                schema: "platform",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuietEndHour",
                schema: "platform",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "QuietHoursEnabled",
                schema: "platform",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuietStartHour",
                schema: "platform",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxMarketingPerDay",
                schema: "platform",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "QuietEndHour",
                schema: "platform",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "QuietHoursEnabled",
                schema: "platform",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "QuietStartHour",
                schema: "platform",
                table: "tenants");
        }
    }
}
