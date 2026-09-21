using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RadicacionBandejaDistribucion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "canal_envio",
                table: "radicados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "radicados",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_respuesta_definitiva",
                table: "radicados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "estado_envio",
                table: "radicados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "folios",
                table: "radicados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "nivel_reserva_id",
                table: "radicados",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "num_anexos",
                table: "radicados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "radicado_relacionado_id",
                table: "radicados",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remitente_documento",
                table: "radicados",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remitente_email",
                table: "radicados",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remitente_telefono",
                table: "radicados",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remitente_tipo_doc",
                table: "radicados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "soporte",
                table: "radicados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "radicados_archivos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    radicado_id = table.Column<long>(type: "bigint", nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    storage_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    fecha_carga = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_radicados_archivos", x => x.id);
                    table.ForeignKey(
                        name: "fk_radicados_archivos_radicados_radicado_id",
                        column: x => x.radicado_id,
                        principalTable: "radicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "radicados_comunicaciones",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    radicado_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: true),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    destino = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    asunto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    detalle = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_radicados_comunicaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_radicados_comunicaciones_radicados_radicado_id",
                        column: x => x.radicado_id,
                        principalTable: "radicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "radicados_tareas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    radicado_id = table.Column<long>(type: "bigint", nullable: false),
                    dependencia_id = table.Column<long>(type: "bigint", nullable: false),
                    funcionario_id = table.Column<long>(type: "bigint", nullable: true),
                    instrucciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    prioridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    origen = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    distribuido_por_id = table.Column<long>(type: "bigint", nullable: true),
                    fecha_asignacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_gestion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observacion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_radicados_tareas", x => x.id);
                    table.ForeignKey(
                        name: "fk_radicados_tareas_org_units_dependencia_id",
                        column: x => x.dependencia_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_radicados_tareas_radicados_radicado_id",
                        column: x => x.radicado_id,
                        principalTable: "radicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "radicados_visibilidad",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_user_id = table.Column<long>(type: "bigint", nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_radicados_visibilidad", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_nivel_reserva_id",
                table: "radicados",
                column: "nivel_reserva_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_radicado_relacionado_id",
                table: "radicados",
                column: "radicado_relacionado_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tenant_id_radicado_relacionado_id",
                table: "radicados",
                columns: new[] { "tenant_id", "radicado_relacionado_id" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_archivos_radicado_id",
                table: "radicados_archivos",
                column: "radicado_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_archivos_tenant_id_radicado_id",
                table: "radicados_archivos",
                columns: new[] { "tenant_id", "radicado_id" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_comunicaciones_radicado_id",
                table: "radicados_comunicaciones",
                column: "radicado_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_comunicaciones_tenant_id_radicado_id",
                table: "radicados_comunicaciones",
                columns: new[] { "tenant_id", "radicado_id" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tareas_dependencia_id",
                table: "radicados_tareas",
                column: "dependencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tareas_radicado_id",
                table: "radicados_tareas",
                column: "radicado_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tareas_tenant_id_dependencia_id_estado",
                table: "radicados_tareas",
                columns: new[] { "tenant_id", "dependencia_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tareas_tenant_id_radicado_id_activa",
                table: "radicados_tareas",
                columns: new[] { "tenant_id", "radicado_id", "activa" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_visibilidad_tenant_id_tenant_user_id",
                table: "radicados_visibilidad",
                columns: new[] { "tenant_id", "tenant_user_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_radicados_niveles_clasificacion_nivel_reserva_id",
                table: "radicados",
                column: "nivel_reserva_id",
                principalTable: "niveles_clasificacion",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_radicados_radicados_radicado_relacionado_id",
                table: "radicados",
                column: "radicado_relacionado_id",
                principalTable: "radicados",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_radicados_niveles_clasificacion_nivel_reserva_id",
                table: "radicados");

            migrationBuilder.DropForeignKey(
                name: "fk_radicados_radicados_radicado_relacionado_id",
                table: "radicados");

            migrationBuilder.DropTable(
                name: "radicados_archivos");

            migrationBuilder.DropTable(
                name: "radicados_comunicaciones");

            migrationBuilder.DropTable(
                name: "radicados_tareas");

            migrationBuilder.DropTable(
                name: "radicados_visibilidad");

            migrationBuilder.DropIndex(
                name: "ix_radicados_nivel_reserva_id",
                table: "radicados");

            migrationBuilder.DropIndex(
                name: "ix_radicados_radicado_relacionado_id",
                table: "radicados");

            migrationBuilder.DropIndex(
                name: "ix_radicados_tenant_id_radicado_relacionado_id",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "canal_envio",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "es_respuesta_definitiva",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "estado_envio",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "folios",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "nivel_reserva_id",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "num_anexos",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "radicado_relacionado_id",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "remitente_documento",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "remitente_email",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "remitente_telefono",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "remitente_tipo_doc",
                table: "radicados");

            migrationBuilder.DropColumn(
                name: "soporte",
                table: "radicados");
        }
    }
}
