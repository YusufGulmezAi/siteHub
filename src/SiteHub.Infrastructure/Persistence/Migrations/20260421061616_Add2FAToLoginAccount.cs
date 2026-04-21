using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add2FAToLoginAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                schema: "identity",
                table: "login_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "two_factor_enabled_at",
                schema: "identity",
                table: "login_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_secret",
                schema: "identity",
                table: "login_accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                collation: "tr_ci_ai");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                schema: "identity",
                table: "login_accounts");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled_at",
                schema: "identity",
                table: "login_accounts");

            migrationBuilder.DropColumn(
                name: "two_factor_secret",
                schema: "identity",
                table: "login_accounts");
        }
    }
}
