using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketingAutomation.Modules.Contacts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contacts");

            migrationBuilder.CreateTable(
                name: "contacts",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CustomAttributes = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "contacts",
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

            migrationBuilder.CreateTable(
                name: "suppression_entries",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppression_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "consent_entries",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConsentText = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consent_entries_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_identifiers",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_identifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_identifiers_contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consent_entries_ContactId_Channel_Purpose",
                schema: "contacts",
                table: "consent_entries",
                columns: new[] { "ContactId", "Channel", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_consent_entries_TenantId",
                schema: "contacts",
                table: "consent_entries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_identifiers_ContactId",
                schema: "contacts",
                table: "contact_identifiers",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_identifiers_TenantId",
                schema: "contacts",
                table: "contact_identifiers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_identifiers_TenantId_Type_Value",
                schema: "contacts",
                table: "contact_identifiers",
                columns: new[] { "TenantId", "Type", "Value" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_Email",
                schema: "contacts",
                table: "contacts",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId",
                schema: "contacts",
                table: "contacts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAt",
                schema: "contacts",
                table: "outbox_messages",
                column: "ProcessedAt",
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_suppression_entries_Channel_Value",
                schema: "contacts",
                table: "suppression_entries",
                columns: new[] { "Channel", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppression_entries_TenantId",
                schema: "contacts",
                table: "suppression_entries",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent_entries",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "contact_identifiers",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "suppression_entries",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "contacts",
                schema: "contacts");
        }
    }
}
