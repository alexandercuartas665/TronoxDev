using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FormulariosRq08 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "form_definitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "form_containers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    container_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    style = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    inline_labels = table.Column<bool>(type: "boolean", nullable: false),
                    tabs_json = table.Column<string>(type: "jsonb", nullable: true),
                    width = table.Column<int>(type: "integer", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_containers", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_containers_form_containers_parent_id",
                        column: x => x.parent_id,
                        principalTable: "form_containers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_form_containers_form_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_responses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_by_tenant_user_id = table.Column<long>(type: "bigint", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_responses_form_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "form_questions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    container_id = table.Column<long>(type: "bigint", nullable: true),
                    field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    help_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    control_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    options_json = table.Column<string>(type: "jsonb", nullable: true),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    grid_col = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    numeral = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    validation_json = table.Column<string>(type: "jsonb", nullable: true),
                    width = table.Column<int>(type: "integer", nullable: false),
                    placeholder_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    default_value = table.Column<string>(type: "text", nullable: true),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    format = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_questions_form_containers_container_id",
                        column: x => x.container_id,
                        principalTable: "form_containers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_form_questions_form_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_form_containers_definition_id_parent_id_sort_order",
                table: "form_containers",
                columns: new[] { "definition_id", "parent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_form_containers_parent_id",
                table: "form_containers",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_definitions_tenant_id_code",
                table: "form_definitions",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_form_definitions_tenant_id_status",
                table: "form_definitions",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_form_questions_container_id",
                table: "form_questions",
                column: "container_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_questions_definition_id_container_id_sort_order",
                table: "form_questions",
                columns: new[] { "definition_id", "container_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_form_questions_definition_id_field_code",
                table: "form_questions",
                columns: new[] { "definition_id", "field_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_form_responses_definition_id_reference",
                table: "form_responses",
                columns: new[] { "definition_id", "reference" });

            migrationBuilder.CreateIndex(
                name: "ix_form_responses_tenant_id_definition_id_status",
                table: "form_responses",
                columns: new[] { "tenant_id", "definition_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_questions");

            migrationBuilder.DropTable(
                name: "form_responses");

            migrationBuilder.DropTable(
                name: "form_containers");

            migrationBuilder.DropTable(
                name: "form_definitions");
        }
    }
}
