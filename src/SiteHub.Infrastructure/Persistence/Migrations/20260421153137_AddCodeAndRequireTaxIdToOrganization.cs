using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeAndRequireTaxIdToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organizations_tax_id",
                schema: "public",
                table: "organizations");

            migrationBuilder.AlterColumn<string>(
                name: "tax_id",
                schema: "public",
                table: "organizations",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "",
                collation: "tr_cs_as",
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11,
                oldNullable: true,
                oldCollation: "tr_cs_as");

            migrationBuilder.AddColumn<long>(
                name: "code",
                schema: "public",
                table: "organizations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_code",
                schema: "public",
                table: "organizations",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_tax_id",
                schema: "public",
                table: "organizations",
                column: "tax_id",
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organizations_code",
                schema: "public",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_organizations_tax_id",
                schema: "public",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "code",
                schema: "public",
                table: "organizations");

            migrationBuilder.AlterColumn<string>(
                name: "tax_id",
                schema: "public",
                table: "organizations",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true,
                collation: "tr_cs_as",
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11,
                oldCollation: "tr_cs_as");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_tax_id",
                schema: "public",
                table: "organizations",
                column: "tax_id",
                unique: true,
                filter: "tax_id IS NOT NULL AND deleted_at IS NULL");
        }
    }
}
