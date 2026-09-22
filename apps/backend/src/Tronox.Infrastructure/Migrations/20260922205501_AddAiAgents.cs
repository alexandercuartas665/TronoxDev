using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_agent_run_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<long>(type: "bigint", nullable: false),
                    agent_id = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    response = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agent_run_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_agents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    disabled_tools_json = table.Column<string>(type: "jsonb", nullable: true),
                    prompt_history_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_cache_fields",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agent_id = table.Column<long>(type: "bigint", nullable: false),
                    field_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_updatable = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agent_cache_fields", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_agent_cache_fields_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_cache_values",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agent_id = table.Column<long>(type: "bigint", nullable: false),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    field_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agent_cache_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_agent_cache_values_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_prompts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agent_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    rule = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agent_prompts", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_agent_prompts_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_resources",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agent_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agent_resources", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_agent_resources_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_cache_fields_agent_id",
                table: "ai_agent_cache_fields",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_cache_fields_tenant_id_agent_id_field_key",
                table: "ai_agent_cache_fields",
                columns: new[] { "tenant_id", "agent_id", "field_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_cache_values_agent_id",
                table: "ai_agent_cache_values",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_cache_values_tenant_id_agent_id_session_id_field_k",
                table: "ai_agent_cache_values",
                columns: new[] { "tenant_id", "agent_id", "session_id", "field_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_prompts_agent_id",
                table: "ai_agent_prompts",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_prompts_tenant_id_agent_id_sort_order",
                table: "ai_agent_prompts",
                columns: new[] { "tenant_id", "agent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_resources_agent_id",
                table: "ai_agent_resources",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_resources_tenant_id_agent_id_sort_order",
                table: "ai_agent_resources",
                columns: new[] { "tenant_id", "agent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_run_logs_tenant_id_agent_id_occurred_at",
                table: "ai_agent_run_logs",
                columns: new[] { "tenant_id", "agent_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_run_logs_tenant_id_conversation_id_occurred_at",
                table: "ai_agent_run_logs",
                columns: new[] { "tenant_id", "conversation_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agents_tenant_id_sort_order",
                table: "ai_agents",
                columns: new[] { "tenant_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_cache_fields");

            migrationBuilder.DropTable(
                name: "ai_agent_cache_values");

            migrationBuilder.DropTable(
                name: "ai_agent_prompts");

            migrationBuilder.DropTable(
                name: "ai_agent_resources");

            migrationBuilder.DropTable(
                name: "ai_agent_run_logs");

            migrationBuilder.DropTable(
                name: "ai_agents");
        }
    }
}
