using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracionArchivistica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "niveles_clasificacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color_etiqueta = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    nivel_orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_niveles_clasificacion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sedes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre_sede = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo_sede = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sigla_sede = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    pais_id = table.Column<long>(type: "bigint", nullable: true),
                    departamento_id = table.Column<long>(type: "bigint", nullable: true),
                    ciudad_id = table.Column<long>(type: "bigint", nullable: true),
                    direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    correo_sede = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sedes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fondos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo_fondo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre_fondo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sede_id = table.Column<long>(type: "bigint", nullable: true),
                    tipo_fondo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_apertura = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_cierre = table.Column<DateOnly>(type: "date", nullable: true),
                    entidad_origen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fondos", x => x.id);
                    table.ForeignKey(
                        name: "fk_fondos_sedes_sede_id",
                        column: x => x.sede_id,
                        principalTable: "sedes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subfondos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fondo_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo_subfondo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre_subfondo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subfondos", x => x.id);
                    table.ForeignKey(
                        name: "fk_subfondos_fondos_fondo_id",
                        column: x => x.fondo_id,
                        principalTable: "fondos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fondos_sede_id",
                table: "fondos",
                column: "sede_id");

            migrationBuilder.CreateIndex(
                name: "ix_fondos_tenant_id_codigo_fondo",
                table: "fondos",
                columns: new[] { "tenant_id", "codigo_fondo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fondos_tenant_id_estado",
                table: "fondos",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_niveles_clasificacion_tenant_id_codigo",
                table: "niveles_clasificacion",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_niveles_clasificacion_tenant_id_nivel_orden",
                table: "niveles_clasificacion",
                columns: new[] { "tenant_id", "nivel_orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sedes_tenant_id_codigo_sede",
                table: "sedes",
                columns: new[] { "tenant_id", "codigo_sede" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sedes_tenant_id_estado",
                table: "sedes",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_subfondos_fondo_id_codigo_subfondo",
                table: "subfondos",
                columns: new[] { "fondo_id", "codigo_subfondo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subfondos_tenant_id_fondo_id",
                table: "subfondos",
                columns: new[] { "tenant_id", "fondo_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "niveles_clasificacion");

            migrationBuilder.DropTable(
                name: "subfondos");

            migrationBuilder.DropTable(
                name: "fondos");

            migrationBuilder.DropTable(
                name: "sedes");
        }
    }
}
