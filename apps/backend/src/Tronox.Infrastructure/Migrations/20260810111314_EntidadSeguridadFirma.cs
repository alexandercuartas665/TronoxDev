using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntidadSeguridadFirma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "firma_configs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    modulo_firma_activo = table.Column<bool>(type: "boolean", nullable: false),
                    ntp_activo = table.Column<bool>(type: "boolean", nullable: false),
                    ntp_servidor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    firma_posicion_default = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    firma_qr_tamano = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    firma_texto_default = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    firma_mostrar_qr = table.Column<bool>(type: "boolean", nullable: false),
                    firma_mostrar_identificacion = table.Column<bool>(type: "boolean", nullable: false),
                    firma_mostrar_nombre = table.Column<bool>(type: "boolean", nullable: false),
                    otp_modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    otp_expiracion_minutos = table.Column<int>(type: "integer", nullable: false),
                    otp_canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    otp_requerido_global = table.Column<bool>(type: "boolean", nullable: false),
                    firma_dias = table.Column<int>(type: "integer", nullable: false),
                    firma_frecuencia_dias = table.Column<int>(type: "integer", nullable: false),
                    firma_masiva_activa = table.Column<bool>(type: "boolean", nullable: false),
                    firma_forzar_lectura = table.Column<bool>(type: "boolean", nullable: false),
                    firma_consentimiento = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firma_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parametros_seguridad",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    intentos_fallidos_max = table.Column<int>(type: "integer", nullable: false),
                    tiempo_inactividad_sesion_min = table.Column<int>(type: "integer", nullable: false),
                    vigencia_contrasena_dias = table.Column<int>(type: "integer", nullable: false),
                    historial_contrasenas = table.Column<int>(type: "integer", nullable: false),
                    longitud_minima_contrasena = table.Column<int>(type: "integer", nullable: false),
                    tiempo_aviso_vencimiento_dias = table.Column<int>(type: "integer", nullable: true),
                    complejidad_contrasena = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parametros_seguridad", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_firma_configs_tenant_id",
                table: "firma_configs",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parametros_seguridad_tenant_id",
                table: "parametros_seguridad",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "firma_configs");

            migrationBuilder.DropTable(
                name: "parametros_seguridad");
        }
    }
}
