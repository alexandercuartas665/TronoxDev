using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FormsFidelidadRq08 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "record_number",
                table: "form_responses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "record_status",
                table: "form_responses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "transaction_date",
                table: "form_responses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "void_reason",
                table: "form_responses",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "voided_at",
                table: "form_responses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "voided_by_tenant_user_id",
                table: "form_responses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aggregate",
                table: "form_questions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "autofill_map_json",
                table: "form_questions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calc_expression",
                table: "form_questions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cascade_config_json",
                table: "form_questions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_dynamic",
                table: "form_questions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "display_field",
                table: "form_questions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "field_visibility_json",
                table: "form_questions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "filter_json",
                table: "form_questions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "presentation",
                table: "form_questions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "source_kind",
                table: "form_questions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "source_ref",
                table: "form_questions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "subform_definition_id",
                table: "form_questions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "value_field",
                table: "form_questions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "card_layout",
                table: "form_definitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "filter_fields_json",
                table: "form_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_mode",
                table: "form_definitions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "identity_source_field_code",
                table: "form_definitions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_module",
                table: "form_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_transactional",
                table: "form_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "list_columns_json",
                table: "form_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "module_icon",
                table: "form_definitions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "module_menu_node_id",
                table: "form_definitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "sequence_id",
                table: "form_definitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unique_key_fields_json",
                table: "form_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "form_field_conditions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    source_field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    set_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_field_conditions", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_field_conditions_form_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_flow_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    form_response_id = table.Column<long>(type: "bigint", nullable: false),
                    workflow_instance_id = table.Column<long>(type: "bigint", nullable: false),
                    workflow_node_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_flow_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_flow_links_form_responses_form_response_id",
                        column: x => x.form_response_id,
                        principalTable: "form_responses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_form_flow_links_workflow_instances_workflow_instance_id",
                        column: x => x.workflow_instance_id,
                        principalTable: "workflow_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_form_flow_links_workflow_nodes_workflow_node_id",
                        column: x => x.workflow_node_id,
                        principalTable: "workflow_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "form_record_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parent_response_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    child_response_id = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_record_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_record_links_form_responses_child_response_id",
                        column: x => x.child_response_id,
                        principalTable: "form_responses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_form_record_links_form_responses_parent_response_id",
                        column: x => x.parent_response_id,
                        principalTable: "form_responses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    single_use = table.Column<bool>(type: "boolean", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    allow_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_tokens_form_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_node_forms",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    node_id = table.Column<long>(type: "bigint", nullable: false),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_node_forms", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_node_forms_form_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_node_forms_workflow_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "workflow_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_form_responses_tenant_id_definition_id_record_status",
                table: "form_responses",
                columns: new[] { "tenant_id", "definition_id", "record_status" });

            migrationBuilder.CreateIndex(
                name: "ix_form_field_conditions_definition_id_sort_order",
                table: "form_field_conditions",
                columns: new[] { "definition_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_form_flow_links_form_response_id",
                table: "form_flow_links",
                column: "form_response_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_flow_links_workflow_instance_id_status",
                table: "form_flow_links",
                columns: new[] { "workflow_instance_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_form_flow_links_workflow_node_id",
                table: "form_flow_links",
                column: "workflow_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_record_links_child_response_id",
                table: "form_record_links",
                column: "child_response_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_record_links_parent_response_id_parent_field_code_sort",
                table: "form_record_links",
                columns: new[] { "parent_response_id", "parent_field_code", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_form_tokens_definition_id",
                table: "form_tokens",
                column: "definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_tokens_tenant_id_token_hash",
                table: "form_tokens",
                columns: new[] { "tenant_id", "token_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_node_forms_definition_id",
                table: "workflow_node_forms",
                column: "definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_node_forms_node_id",
                table: "workflow_node_forms",
                column: "node_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_field_conditions");

            migrationBuilder.DropTable(
                name: "form_flow_links");

            migrationBuilder.DropTable(
                name: "form_record_links");

            migrationBuilder.DropTable(
                name: "form_tokens");

            migrationBuilder.DropTable(
                name: "workflow_node_forms");

            migrationBuilder.DropIndex(
                name: "ix_form_responses_tenant_id_definition_id_record_status",
                table: "form_responses");

            migrationBuilder.DropColumn(
                name: "record_number",
                table: "form_responses");

            migrationBuilder.DropColumn(
                name: "record_status",
                table: "form_responses");

            migrationBuilder.DropColumn(
                name: "transaction_date",
                table: "form_responses");

            migrationBuilder.DropColumn(
                name: "void_reason",
                table: "form_responses");

            migrationBuilder.DropColumn(
                name: "voided_at",
                table: "form_responses");

            migrationBuilder.DropColumn(
                name: "voided_by_tenant_user_id",
                table: "form_responses");

            migrationBuilder.DropColumn(
                name: "aggregate",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "autofill_map_json",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "calc_expression",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "cascade_config_json",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "default_dynamic",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "display_field",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "field_visibility_json",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "filter_json",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "presentation",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "source_kind",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "source_ref",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "subform_definition_id",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "value_field",
                table: "form_questions");

            migrationBuilder.DropColumn(
                name: "card_layout",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "filter_fields_json",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "identity_mode",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "identity_source_field_code",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "is_module",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "is_transactional",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "list_columns_json",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "module_icon",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "module_menu_node_id",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "sequence_id",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "unique_key_fields_json",
                table: "form_definitions");
        }
    }
}
