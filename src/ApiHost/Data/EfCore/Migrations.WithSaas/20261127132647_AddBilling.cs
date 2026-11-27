using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiHost.Data.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261127132647_AddBilling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BillingAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PlanId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PlanVersion = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                TrialEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CurrentPeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CancelAtPeriodEnd = table.Column<bool>(type: "boolean", nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                CustomerId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                SubscriptionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingAccounts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PeriodUsage",
            columns: table => new
            {
                OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                LimitName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Used = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PeriodUsage", x => new { x.OrganisationId, x.LimitName, x.PeriodStart });
            });

        migrationBuilder.CreateIndex(
            name: "IX_BillingAccounts_CustomerId",
            table: "BillingAccounts",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_BillingAccounts_SubscriptionId",
            table: "BillingAccounts",
            column: "SubscriptionId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BillingAccounts");

        migrationBuilder.DropTable(
            name: "PeriodUsage");
    }
}
