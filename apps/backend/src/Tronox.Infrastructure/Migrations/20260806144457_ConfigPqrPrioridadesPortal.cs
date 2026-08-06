using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigPqrPrioridadesPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rad_portal_configs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre_entidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subtitulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    max_adjunto_mb = table.Column<int>(type: "integer", nullable: false),
                    banner = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    permitir_anonimo = table.Column<bool>(type: "boolean", nullable: false),
                    exigir_captcha = table.Column<bool>(type: "boolean", nullable: false),
                    canales_atencion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    aviso_privacidad = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    faq = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rad_portal_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rad_prioridades",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    icono = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    sla_sugerido = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rad_prioridades", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rad_portal_configs_slug",
                table: "rad_portal_configs",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rad_portal_configs_tenant_id",
                table: "rad_portal_configs",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rad_prioridades_tenant_id_codigo",
                table: "rad_prioridades",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rad_portal_configs");

            migrationBuilder.DropTable(
                name: "rad_prioridades");
        }
    }
}
