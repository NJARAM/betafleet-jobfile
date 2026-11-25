using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BetaFleet.Api.Data.Migrations;

/// <inheritdoc />
public partial class _20261125081932_Auth : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Username = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Role = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                CustomerId = table.Column<int>(type: "integer", nullable: true),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                FailedLogins = table.Column<int>(type: "integer", nullable: false),
                LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TokenVersion = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
                table.ForeignKey(
                    name: "FK_Users_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_CustomerId",
            table: "Users",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Users_Username",
            table: "Users",
            column: "Username",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Users");
    }
}
