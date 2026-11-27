using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiHost.Data.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261127144039_AddAuditLog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                ActorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                TargetType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                TargetId = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Details = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEntries", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_At",
            table: "AuditEntries",
            column: "At");

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_TargetId",
            table: "AuditEntries",
            column: "TargetId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditEntries");
    }
}
