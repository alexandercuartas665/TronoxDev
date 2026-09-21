using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigRadicacionRf01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "migraciones_radicados",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha_migracion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ejecutada_por_tenant_user_id = table.Column<long>(type: "bigint", nullable: true),
                    archivo_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cantidad_total = table.Column<int>(type: "integer", nullable: false),
                    cantidad_exitosos = table.Column<int>(type: "integer", nullable: false),
                    cantidad_errores = table.Column<int>(type: "integer", nullable: false),
                    estado_destino = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reporte_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_migraciones_radicados", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notificaciones_radicacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    evento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    destinatarios_roles_json = table.Column<string>(type: "jsonb", nullable: true),
                    destinatarios_usuarios_json = table.Column<string>(type: "jsonb", nullable: true),
                    plantilla_asunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    plantilla_cuerpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificaciones_radicacion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "radicacion_configs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    consecutivo_entrada_inicio = table.Column<int>(type: "integer", nullable: false),
                    consecutivo_salida_inicio = table.Column<int>(type: "integer", nullable: false),
                    consecutivo_interno_inicio = table.Column<int>(type: "integer", nullable: false),
                    reinicio_anual = table.Column<bool>(type: "boolean", nullable: false),
                    digitos_consecutivo = table.Column<int>(type: "integer", nullable: false),
                    separador = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    alerta1porcentaje = table.Column<int>(type: "integer", nullable: false),
                    alerta2porcentaje = table.Column<int>(type: "integer", nullable: false),
                    alerta_tutela_horas = table.Column<int>(type: "integer", nullable: false),
                    notificar_jefe_al_vencer = table.Column<bool>(type: "boolean", nullable: false),
                    notificar_direccion_al_vencer = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_radicacion_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipos_comunicacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direccion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    es_pqrsd = table.Column<bool>(type: "boolean", nullable: false),
                    es_tutela = table.Column<bool>(type: "boolean", nullable: false),
                    es_recurso = table.Column<bool>(type: "boolean", nullable: false),
                    requiere_respuesta = table.Column<bool>(type: "boolean", nullable: false),
                    dias_respuesta = table.Column<int>(type: "integer", nullable: true),
                    tipo_dia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    inicio_termino = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    prorrogable = table.Column<bool>(type: "boolean", nullable: false),
                    dias_prorroga = table.Column<int>(type: "integer", nullable: true),
                    permite_anonimo = table.Column<bool>(type: "boolean", nullable: false),
                    habilitado_web = table.Column<bool>(type: "boolean", nullable: false),
                    nivel_reserva_default_id = table.Column<long>(type: "bigint", nullable: true),
                    icono = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    palabras_clave = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    orden_portal = table.Column<int>(type: "integer", nullable: true),
                    descripcion_ciudadano = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_comunicacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_tipos_comunicacion_niveles_clasificacion_nivel_reserva_defa",
                        column: x => x.nivel_reserva_default_id,
                        principalTable: "niveles_clasificacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "buzones_correo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre_buzon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direccion_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    protocolo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    servidor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    puerto = table.Column<int>(type: "integer", nullable: true),
                    seguridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    usuario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contrasena_encrypted = table.Column<string>(type: "text", nullable: true),
                    carpeta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    frecuencia_revision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    modo_radicacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tiempo_espera_minutos = table.Column<int>(type: "integer", nullable: true),
                    tipo_comunicacion_default_id = table.Column<long>(type: "bigint", nullable: true),
                    dependencia_default_id = table.Column<long>(type: "bigint", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buzones_correo", x => x.id);
                    table.ForeignKey(
                        name: "fk_buzones_correo_org_units_dependencia_default_id",
                        column: x => x.dependencia_default_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buzones_correo_tipos_comunicacion_tipo_comunicacion_default",
                        column: x => x.tipo_comunicacion_default_id,
                        principalTable: "tipos_comunicacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_buzones_correo_dependencia_default_id",
                table: "buzones_correo",
                column: "dependencia_default_id");

            migrationBuilder.CreateIndex(
                name: "ix_buzones_correo_tenant_id_activo",
                table: "buzones_correo",
                columns: new[] { "tenant_id", "activo" });

            migrationBuilder.CreateIndex(
                name: "ix_buzones_correo_tipo_comunicacion_default_id",
                table: "buzones_correo",
                column: "tipo_comunicacion_default_id");

            migrationBuilder.CreateIndex(
                name: "ix_migraciones_radicados_tenant_id_fecha_migracion",
                table: "migraciones_radicados",
                columns: new[] { "tenant_id", "fecha_migracion" });

            migrationBuilder.CreateIndex(
                name: "ix_notificaciones_radicacion_tenant_id_evento",
                table: "notificaciones_radicacion",
                columns: new[] { "tenant_id", "evento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_radicacion_configs_tenant_id",
                table: "radicacion_configs",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipos_comunicacion_nivel_reserva_default_id",
                table: "tipos_comunicacion",
                column: "nivel_reserva_default_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_comunicacion_tenant_id_codigo",
                table: "tipos_comunicacion",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipos_comunicacion_tenant_id_direccion_activo",
                table: "tipos_comunicacion",
                columns: new[] { "tenant_id", "direccion", "activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buzones_correo");

            migrationBuilder.DropTable(
                name: "migraciones_radicados");

            migrationBuilder.DropTable(
                name: "notificaciones_radicacion");

            migrationBuilder.DropTable(
                name: "radicacion_configs");

            migrationBuilder.DropTable(
                name: "tipos_comunicacion");
        }
    }
}
