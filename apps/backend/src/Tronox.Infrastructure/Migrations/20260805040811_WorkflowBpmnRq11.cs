using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowBpmnRq11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    process_code = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    bpmn_xml = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_instances",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_cycle = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_instances", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_instances_workflow_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_nodes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    bpmn_element_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    node_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    step_number = table.Column<int>(type: "integer", nullable: true),
                    allows_assignment = table.Column<bool>(type: "boolean", nullable: false),
                    restart_node_id = table.Column<long>(type: "bigint", nullable: true),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    w = table.Column<int>(type: "integer", nullable: true),
                    h = table.Column<int>(type: "integer", nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_nodes_workflow_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflow_nodes_workflow_nodes_restart_node_id",
                        column: x => x.restart_node_id,
                        principalTable: "workflow_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_edges",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    definition_id = table.Column<long>(type: "bigint", nullable: false),
                    source_node_id = table.Column<long>(type: "bigint", nullable: false),
                    target_node_id = table.Column<long>(type: "bigint", nullable: false),
                    bpmn_element_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    condition_expression = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_edges", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_edges_workflow_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflow_edges_workflow_nodes_source_node_id",
                        column: x => x.source_node_id,
                        principalTable: "workflow_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_edges_workflow_nodes_target_node_id",
                        column: x => x.target_node_id,
                        principalTable: "workflow_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_node_policies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workflow_node_id = table.Column<long>(type: "bigint", nullable: false),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_node_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_node_policies_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_node_policies_workflow_nodes_workflow_node_id",
                        column: x => x.workflow_node_id,
                        principalTable: "workflow_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_step_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instance_id = table.Column<long>(type: "bigint", nullable: false),
                    node_id = table.Column<long>(type: "bigint", nullable: false),
                    cycle_index = table.Column<int>(type: "integer", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    assigned_to_tenant_user_id = table.Column<long>(type: "bigint", nullable: true),
                    executed_by_tenant_user_id = table.Column<long>(type: "bigint", nullable: true),
                    is_cycle_start = table.Column<bool>(type: "boolean", nullable: false),
                    approval_result = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    approval_comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_step_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_step_histories_workflow_instances_instance_id",
                        column: x => x.instance_id,
                        principalTable: "workflow_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflow_step_histories_workflow_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "workflow_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id_process_code_is_published",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "process_code", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id_process_code_version",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "process_code", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_edges_definition_id",
                table: "workflow_edges",
                column: "definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_edges_source_node_id",
                table: "workflow_edges",
                column: "source_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_edges_target_node_id",
                table: "workflow_edges",
                column: "target_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_instances_definition_id",
                table: "workflow_instances",
                column: "definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_instances_tenant_id_status",
                table: "workflow_instances",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_node_policies_org_unit_id",
                table: "workflow_node_policies",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_node_policies_workflow_node_id_org_unit_id",
                table: "workflow_node_policies",
                columns: new[] { "workflow_node_id", "org_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_nodes_definition_id_bpmn_element_id",
                table: "workflow_nodes",
                columns: new[] { "definition_id", "bpmn_element_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_nodes_restart_node_id",
                table: "workflow_nodes",
                column: "restart_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_histories_instance_id_is_current",
                table: "workflow_step_histories",
                columns: new[] { "instance_id", "is_current" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_histories_node_id",
                table: "workflow_step_histories",
                column: "node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_edges");

            migrationBuilder.DropTable(
                name: "workflow_node_policies");

            migrationBuilder.DropTable(
                name: "workflow_step_histories");

            migrationBuilder.DropTable(
                name: "workflow_instances");

            migrationBuilder.DropTable(
                name: "workflow_nodes");

            migrationBuilder.DropTable(
                name: "workflow_definitions");
        }
    }
}
