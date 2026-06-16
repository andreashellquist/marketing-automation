using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketingAutomation.Modules.Journeys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialJourneys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "journeys");

            migrationBuilder.CreateTable(
                name: "journey_runs",
                schema: "journeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JourneyId = table.Column<Guid>(type: "uuid", nullable: false),
                    JourneyVersion = table.Column<int>(type: "integer", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    RecipientTimezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CurrentNodeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WakeUpAtTicks = table.Column<long>(type: "bigint", nullable: true),
                    WaitEventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journey_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "journeys",
                schema: "journeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ReentryPolicy = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    StartNodeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Nodes = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "journeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journey_runs_ContactId_WaitEventName",
                schema: "journeys",
                table: "journey_runs",
                columns: new[] { "ContactId", "WaitEventName" });

            migrationBuilder.CreateIndex(
                name: "IX_journey_runs_JourneyId_ContactId_Status",
                schema: "journeys",
                table: "journey_runs",
                columns: new[] { "JourneyId", "ContactId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_journey_runs_Status_WakeUpAtTicks",
                schema: "journeys",
                table: "journey_runs",
                columns: new[] { "Status", "WakeUpAtTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_journey_runs_TenantId",
                schema: "journeys",
                table: "journey_runs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_journeys_TenantId",
                schema: "journeys",
                table: "journeys",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAt",
                schema: "journeys",
                table: "outbox_messages",
                column: "ProcessedAt",
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journey_runs",
                schema: "journeys");

            migrationBuilder.DropTable(
                name: "journeys",
                schema: "journeys");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "journeys");
        }
    }
}
