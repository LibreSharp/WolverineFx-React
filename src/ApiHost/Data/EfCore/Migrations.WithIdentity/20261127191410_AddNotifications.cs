using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiHost.Data.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261127191410_AddNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotificationPreferences",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                EmailOff = table.Column<string[]>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UserId_CreatedAt",
            table: "Notifications",
            columns: new[] { "UserId", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NotificationPreferences");

        migrationBuilder.DropTable(
            name: "Notifications");
    }
}
