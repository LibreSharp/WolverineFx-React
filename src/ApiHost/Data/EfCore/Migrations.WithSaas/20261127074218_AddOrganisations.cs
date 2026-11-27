using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiHost.Data.EfCore.Migrations;

/// <inheritdoc />
public partial class _20261127074218_AddOrganisations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "tenant_id",
            table: "Todos",
            type: "text",
            nullable: true,
            defaultValue: "*DEFAULT*");

        migrationBuilder.AddColumn<Guid>(
            name: "OrganisationId",
            table: "RefreshTokens",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Memberships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "text", nullable: false),
                Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Memberships", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OrganisationInvitations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                InvitedByUserId = table.Column<string>(type: "text", nullable: false),
                TokenHash = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganisationInvitations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Organisations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organisations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Todos_tenant_id",
            table: "Todos",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "IX_Memberships_OrganisationId_UserId",
            table: "Memberships",
            columns: new[] { "OrganisationId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Memberships_UserId",
            table: "Memberships",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganisationInvitations_OrganisationId_NormalizedEmail",
            table: "OrganisationInvitations",
            columns: new[] { "OrganisationId", "NormalizedEmail" });

        migrationBuilder.CreateIndex(
            name: "IX_OrganisationInvitations_TokenHash",
            table: "OrganisationInvitations",
            column: "TokenHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Memberships");

        migrationBuilder.DropTable(
            name: "OrganisationInvitations");

        migrationBuilder.DropTable(
            name: "Organisations");

        migrationBuilder.DropIndex(
            name: "IX_Todos_tenant_id",
            table: "Todos");

        migrationBuilder.DropColumn(
            name: "tenant_id",
            table: "Todos");

        migrationBuilder.DropColumn(
            name: "OrganisationId",
            table: "RefreshTokens");
    }
}
