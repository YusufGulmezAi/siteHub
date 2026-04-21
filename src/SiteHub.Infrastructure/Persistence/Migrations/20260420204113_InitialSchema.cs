using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "geography");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:tr_ci_ai", "tr-TR-u-ks-level1,tr-TR-u-ks-level1,icu,False")
                .Annotation("Npgsql:CollationDefinition:tr_cs_as", "tr-TR-x-icu,tr-TR-x-icu,icu,True");

            migrationBuilder.CreateSequence(
                name: "seq_organization_code",
                schema: "public");

            migrationBuilder.CreateSequence(
                name: "seq_site_code",
                schema: "public");

            migrationBuilder.CreateSequence(
                name: "seq_unit_code",
                schema: "public");

            migrationBuilder.CreateSequence(
                name: "seq_unit_period_code",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "countries",
                schema: "geography",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    iso_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, collation: "tr_cs_as"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr_ci_ai"),
                    phone_prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, collation: "tr_ci_ai"),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entity_changes",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr_ci_ai"),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true, collation: "tr_ci_ai"),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "tr_ci_ai"),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "tr_ci_ai"),
                    context_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "tr_ci_ai"),
                    context_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_changes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr_ci_ai"),
                    commercial_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, collation: "tr_ci_ai"),
                    tax_id = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true, collation: "tr_cs_as"),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "tr_ci_ai"),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "tr_ci_ai"),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true, collation: "tr_ci_ai"),
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
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "tr_cs_as"),
                    resource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "tr_ci_ai"),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "tr_ci_ai"),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, collation: "tr_ci_ai"),
                    deprecated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                schema: "geography",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr_ci_ai"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "tr_cs_as"),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regions", x => x.id);
                    table.ForeignKey(
                        name: "FK_regions_countries_country_id",
                        column: x => x.country_id,
                        principalSchema: "geography",
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "tr_ci_ai"),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "tr_ci_ai"),
                    scope = table.Column<short>(type: "smallint", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    delete_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "tr_ci_ai")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "public",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provinces",
                schema: "geography",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    region_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr_ci_ai"),
                    plate_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, collation: "tr_cs_as")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provinces", x => x.id);
                    table.ForeignKey(
                        name: "FK_provinces_regions_region_id",
                        column: x => x.region_id,
                        principalSchema: "geography",
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "identity",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "districts",
                schema: "geography",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr_ci_ai")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_districts", x => x.id);
                    table.ForeignKey(
                        name: "FK_districts_provinces_province_id",
                        column: x => x.province_id,
                        principalSchema: "geography",
                        principalTable: "provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "neighborhoods",
                schema: "geography",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    district_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, collation: "tr_ci_ai"),
                    postal_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true, collation: "tr_cs_as")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_neighborhoods", x => x.id);
                    table.ForeignKey(
                        name: "FK_neighborhoods_districts_district_id",
                        column: x => x.district_id,
                        principalSchema: "geography",
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "addresses",
                schema: "geography",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    neighborhood_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_line_1 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, collation: "tr_ci_ai"),
                    address_line_2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "tr_ci_ai"),
                    postal_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true, collation: "tr_cs_as"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    delete_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "tr_ci_ai")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addresses", x => x.id);
                    table.ForeignKey(
                        name: "FK_addresses_neighborhoods_neighborhood_id",
                        column: x => x.neighborhood_id,
                        principalSchema: "geography",
                        principalTable: "neighborhoods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    national_id = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false, collation: "tr_cs_as"),
                    person_type = table.Column<short>(type: "smallint", nullable: false),
                    full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, collation: "tr_ci_ai"),
                    mobile_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, collation: "tr_cs_as"),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true, collation: "tr_cs_as"),
                    kep_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true, collation: "tr_cs_as"),
                    profile_photo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "tr_ci_ai"),
                    notification_address_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_persons", x => x.id);
                    table.ForeignKey(
                        name: "FK_persons_addresses_notification_address_id",
                        column: x => x.notification_address_id,
                        principalSchema: "geography",
                        principalTable: "addresses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "login_accounts",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false, collation: "tr_cs_as"),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, collation: "tr_ci_ai"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ip_whitelist = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, collation: "tr_ci_ai"),
                    login_schedule_json = table.Column<string>(type: "jsonb", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true, collation: "tr_ci_ai"),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_by_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "tr_ci_ai"),
                    delete_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "tr_ci_ai")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_login_accounts_persons_person_id",
                        column: x => x.person_id,
                        principalSchema: "identity",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    context_type = table.Column<short>(type: "smallint", nullable: false),
                    context_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    delete_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "tr_ci_ai")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_memberships_login_accounts_login_account_id",
                        column: x => x.login_account_id,
                        principalSchema: "identity",
                        principalTable: "login_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_memberships_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_addresses_deleted_at",
                schema: "geography",
                table: "addresses",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_neighborhood_id",
                schema: "geography",
                table: "addresses",
                column: "neighborhood_id");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_postal_code",
                schema: "geography",
                table: "addresses",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "IX_countries_display_order",
                schema: "geography",
                table: "countries",
                column: "display_order");

            migrationBuilder.CreateIndex(
                name: "IX_countries_iso_code",
                schema: "geography",
                table: "countries",
                column: "iso_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_districts_external_id",
                schema: "geography",
                table: "districts",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_districts_province_id",
                schema: "geography",
                table: "districts",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "IX_districts_province_id_name",
                schema: "geography",
                table: "districts",
                columns: new[] { "province_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_entity_changes_correlation_id",
                schema: "audit",
                table: "entity_changes",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_entity_changes_entity_type_entity_id",
                schema: "audit",
                table: "entity_changes",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_entity_changes_timestamp",
                schema: "audit",
                table: "entity_changes",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_entity_changes_user_id",
                schema: "audit",
                table: "entity_changes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_login_accounts_deleted_at",
                schema: "identity",
                table: "login_accounts",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_login_accounts_is_active",
                schema: "identity",
                table: "login_accounts",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_login_accounts_lockout_until",
                schema: "identity",
                table: "login_accounts",
                column: "lockout_until");

            migrationBuilder.CreateIndex(
                name: "IX_login_accounts_login_email",
                schema: "identity",
                table: "login_accounts",
                column: "login_email",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_login_accounts_person_id",
                schema: "identity",
                table: "login_accounts",
                column: "person_id",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_context_type_context_id",
                schema: "identity",
                table: "memberships",
                columns: new[] { "context_type", "context_id" });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_deleted_at",
                schema: "identity",
                table: "memberships",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_login_account_id",
                schema: "identity",
                table: "memberships",
                column: "login_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_login_account_id_is_active",
                schema: "identity",
                table: "memberships",
                columns: new[] { "login_account_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_role_id",
                schema: "identity",
                table: "memberships",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_neighborhoods_district_id",
                schema: "geography",
                table: "neighborhoods",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "IX_neighborhoods_district_id_external_id",
                schema: "geography",
                table: "neighborhoods",
                columns: new[] { "district_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_neighborhoods_name",
                schema: "geography",
                table: "neighborhoods",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_neighborhoods_postal_code",
                schema: "geography",
                table: "neighborhoods",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_deleted_at",
                schema: "public",
                table: "organizations",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_is_active",
                schema: "public",
                table: "organizations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_name",
                schema: "public",
                table: "organizations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_search_text",
                schema: "public",
                table: "organizations",
                column: "search_text");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_tax_id",
                schema: "public",
                table: "organizations",
                column: "tax_id",
                unique: true,
                filter: "tax_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_deprecated_at",
                schema: "identity",
                table: "permissions",
                column: "deprecated_at");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_key",
                schema: "identity",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_resource",
                schema: "identity",
                table: "permissions",
                column: "resource");

            migrationBuilder.CreateIndex(
                name: "IX_persons_deleted_at",
                schema: "identity",
                table: "persons",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_persons_email",
                schema: "identity",
                table: "persons",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_persons_is_active",
                schema: "identity",
                table: "persons",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_persons_mobile_phone",
                schema: "identity",
                table: "persons",
                column: "mobile_phone");

            migrationBuilder.CreateIndex(
                name: "IX_persons_national_id",
                schema: "identity",
                table: "persons",
                column: "national_id",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_persons_notification_address_id",
                schema: "identity",
                table: "persons",
                column: "notification_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_persons_search_text",
                schema: "identity",
                table: "persons",
                column: "search_text");

            migrationBuilder.CreateIndex(
                name: "IX_provinces_external_id",
                schema: "geography",
                table: "provinces",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provinces_name",
                schema: "geography",
                table: "provinces",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_provinces_region_id",
                schema: "geography",
                table: "provinces",
                column: "region_id");

            migrationBuilder.CreateIndex(
                name: "IX_regions_country_id",
                schema: "geography",
                table: "regions",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "IX_regions_country_id_code",
                schema: "geography",
                table: "regions",
                columns: new[] { "country_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                schema: "identity",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                schema: "identity",
                table: "roles",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_roles_organization_id",
                schema: "identity",
                table: "roles",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_scope_is_system",
                schema: "identity",
                table: "roles",
                columns: new[] { "scope", "is_system" });

            migrationBuilder.CreateIndex(
                name: "IX_roles_service_organization_id",
                schema: "identity",
                table: "roles",
                column: "service_organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_changes",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "login_accounts",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "persons",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "addresses",
                schema: "geography");

            migrationBuilder.DropTable(
                name: "neighborhoods",
                schema: "geography");

            migrationBuilder.DropTable(
                name: "districts",
                schema: "geography");

            migrationBuilder.DropTable(
                name: "provinces",
                schema: "geography");

            migrationBuilder.DropTable(
                name: "regions",
                schema: "geography");

            migrationBuilder.DropTable(
                name: "countries",
                schema: "geography");

            migrationBuilder.DropSequence(
                name: "seq_organization_code",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "seq_site_code",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "seq_unit_code",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "seq_unit_period_code",
                schema: "public");
        }
    }
}
