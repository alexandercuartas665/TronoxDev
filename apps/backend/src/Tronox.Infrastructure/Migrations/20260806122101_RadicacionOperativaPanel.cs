using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RadicacionOperativaPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "correos_recibidos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    buzon_correo_id = table.Column<long>(type: "bigint", nullable: true),
                    buzon_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    remitente = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    asunto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_recepcion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    radicado_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correos_recibidos", x => x.id);
                    table.ForeignKey(
                        name: "fk_correos_recibidos_buzones_correo_buzon_correo_id",
                        column: x => x.buzon_correo_id,
                        principalTable: "buzones_correo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "radicados",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_radicado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prioridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo_comunicacion_id = table.Column<long>(type: "bigint", nullable: true),
                    asunto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    remitente_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    anonimo = table.Column<bool>(type: "boolean", nullable: false),
                    dependencia_destino_id = table.Column<long>(type: "bigint", nullable: true),
                    dependencia_origen_id = table.Column<long>(type: "bigint", nullable: true),
                    funcionario_asignado_id = table.Column<long>(type: "bigint", nullable: true),
                    funcionario_origen_id = table.Column<long>(type: "bigint", nullable: true),
                    usuario_radica_id = table.Column<long>(type: "bigint", nullable: true),
                    fecha_radicacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_distribucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_radicados", x => x.id);
                    table.ForeignKey(
                        name: "fk_radicados_org_units_dependencia_destino_id",
                        column: x => x.dependencia_destino_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_radicados_org_units_dependencia_origen_id",
                        column: x => x.dependencia_origen_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_radicados_tipos_comunicacion_tipo_comunicacion_id",
                        column: x => x.tipo_comunicacion_id,
                        principalTable: "tipos_comunicacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "radicados_trazabilidad",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    radicado_id = table.Column<long>(type: "bigint", nullable: false),
                    accion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: true),
                    detalle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_radicados_trazabilidad", x => x.id);
                    table.ForeignKey(
                        name: "fk_radicados_trazabilidad_radicados_radicado_id",
                        column: x => x.radicado_id,
                        principalTable: "radicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_correos_recibidos_buzon_correo_id",
                table: "correos_recibidos",
                column: "buzon_correo_id");

            migrationBuilder.CreateIndex(
                name: "ix_correos_recibidos_tenant_id_estado",
                table: "correos_recibidos",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_dependencia_destino_id",
                table: "radicados",
                column: "dependencia_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_dependencia_origen_id",
                table: "radicados",
                column: "dependencia_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tenant_id_estado",
                table: "radicados",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tenant_id_fecha_radicacion",
                table: "radicados",
                columns: new[] { "tenant_id", "fecha_radicacion" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tenant_id_fecha_vencimiento",
                table: "radicados",
                columns: new[] { "tenant_id", "fecha_vencimiento" });

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tenant_id_numero_radicado",
                table: "radicados",
                columns: new[] { "tenant_id", "numero_radicado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_radicados_tipo_comunicacion_id",
                table: "radicados",
                column: "tipo_comunicacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_trazabilidad_radicado_id",
                table: "radicados_trazabilidad",
                column: "radicado_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicados_trazabilidad_tenant_id_radicado_id_accion",
                table: "radicados_trazabilidad",
                columns: new[] { "tenant_id", "radicado_id", "accion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "correos_recibidos");

            migrationBuilder.DropTable(
                name: "radicados_trazabilidad");

            migrationBuilder.DropTable(
                name: "radicados");
        }
    }
}
