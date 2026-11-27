using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiHost.Data.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261127144105_AddBackOffice : Migration
{
    // The AuditEntries table comes from AddAuditLog (shared with --Auth identity apps).
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AdminNotes",
            table: "Organisations",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SuspendedAt",
            table: "Organisations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SuspensionReason",
            table: "Organisations",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "FeatureOverrides",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Feature = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ScopeKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FeatureOverrides", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FeatureOverrides");

        migrationBuilder.DropColumn(
            name: "AdminNotes",
            table: "Organisations");

        migrationBuilder.DropColumn(
            name: "SuspendedAt",
            table: "Organisations");

        migrationBuilder.DropColumn(
            name: "SuspensionReason",
            table: "Organisations");
    }
}
