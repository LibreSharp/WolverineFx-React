using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiHost.Data.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261127191433_AddIntegrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApiKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Hint = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKeys", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "IncomingWebhooks",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncomingWebhooks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OrganisationSecrets",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(110)", maxLength: 110, nullable: false),
                OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ProtectedValue = table.Column<string>(type: "text", nullable: false),
                Hint = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganisationSecrets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WebhookDeliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                StatusCode = table.Column<int>(type: "integer", nullable: true),
                Response = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WebhookSubscriptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Events = table.Column<string[]>(type: "text[]", nullable: false),
                ProtectedSecret = table.Column<string>(type: "text", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_OrganisationId",
            table: "ApiKeys",
            column: "OrganisationId");

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_SecretHash",
            table: "ApiKeys",
            column: "SecretHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganisationSecrets_OrganisationId",
            table: "OrganisationSecrets",
            column: "OrganisationId");

        migrationBuilder.CreateIndex(
            name: "IX_WebhookDeliveries_OrganisationId_CreatedAt",
            table: "WebhookDeliveries",
            columns: new[] { "OrganisationId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_WebhookDeliveries_SubscriptionId_CreatedAt",
            table: "WebhookDeliveries",
            columns: new[] { "SubscriptionId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_WebhookSubscriptions_OrganisationId",
            table: "WebhookSubscriptions",
            column: "OrganisationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApiKeys");

        migrationBuilder.DropTable(
            name: "IncomingWebhooks");

        migrationBuilder.DropTable(
            name: "OrganisationSecrets");

        migrationBuilder.DropTable(
            name: "WebhookDeliveries");

        migrationBuilder.DropTable(
            name: "WebhookSubscriptions");
    }
}
