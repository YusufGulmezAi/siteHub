using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSitesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "sites",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr_ci_ai"),
                    commercial_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "tr_ci_ai"),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, collation: "tr_ci_ai"),
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    district_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iban = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true, collation: "tr_cs_as"),
                    tax_id = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true, collation: "tr_cs_as"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    delete_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "tr_ci_ai"),
                    search_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, collation: "tr_cs_as")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sites", x => x.id);
                    table.ForeignKey(
                        name: "FK_sites_districts_district_id",
                        column: x => x.district_id,
                        principalSchema: "geography",
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sites_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "public",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sites_provinces_province_id",
                        column: x => x.province_id,
                        principalSchema: "geography",
                        principalTable: "provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sites_district_id",
                schema: "tenancy",
                table: "sites",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "IX_sites_province_id",
                schema: "tenancy",
                table: "sites",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_sites_code_unique",
                schema: "tenancy",
                table: "sites",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sites_deleted_at",
                schema: "tenancy",
                table: "sites",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_sites_is_active",
                schema: "tenancy",
                table: "sites",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_sites_org_name_unique",
                schema: "tenancy",
                table: "sites",
                columns: new[] { "organization_id", "name" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sites_organization_id",
                schema: "tenancy",
                table: "sites",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sites_search_text",
                schema: "tenancy",
                table: "sites",
                column: "search_text");

            migrationBuilder.CreateIndex(
                name: "ix_sites_tax_id",
                schema: "tenancy",
                table: "sites",
                column: "tax_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sites",
                schema: "tenancy");
        }
    }
}
