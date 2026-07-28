using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConstruccionTrdRf04 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trd_asignaciones",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trd_version_id = table.Column<long>(type: "bigint", nullable: false),
                    dependencia_org_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    serie_documental_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo_ccd = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tiempo_gestion = table.Column<int>(type: "integer", nullable: false),
                    tiempo_central = table.Column<int>(type: "integer", nullable: false),
                    disposicion_final = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reproduccion_tecnica = table.Column<bool>(type: "boolean", nullable: false),
                    serie_ddhh_dih = table.Column<bool>(type: "boolean", nullable: false),
                    procedimiento = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    nivel_clasificacion_id = table.Column<long>(type: "bigint", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trd_asignaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_trd_asignaciones_niveles_clasificacion_nivel_clasificacion_",
                        column: x => x.nivel_clasificacion_id,
                        principalTable: "niveles_clasificacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trd_asignaciones_org_units_dependencia_org_unit_id",
                        column: x => x.dependencia_org_unit_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trd_asignaciones_series_documentales_serie_documental_id",
                        column: x => x.serie_documental_id,
                        principalTable: "series_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trd_asignaciones_trd_versiones_trd_version_id",
                        column: x => x.trd_version_id,
                        principalTable: "trd_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trd_metadatos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trd_asignacion_id = table.Column<long>(type: "bigint", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_dato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    lista_maestra_id = table.Column<long>(type: "bigint", nullable: true),
                    contexto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trd_metadatos", x => x.id);
                    table.ForeignKey(
                        name: "fk_trd_metadatos_listas_maestras_lista_maestra_id",
                        column: x => x.lista_maestra_id,
                        principalTable: "listas_maestras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trd_metadatos_trd_asignaciones_trd_asignacion_id",
                        column: x => x.trd_asignacion_id,
                        principalTable: "trd_asignaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trd_asignaciones_dependencia_org_unit_id",
                table: "trd_asignaciones",
                column: "dependencia_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_trd_asignaciones_nivel_clasificacion_id",
                table: "trd_asignaciones",
                column: "nivel_clasificacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_trd_asignaciones_serie_documental_id",
                table: "trd_asignaciones",
                column: "serie_documental_id");

            migrationBuilder.CreateIndex(
                name: "ix_trd_asignaciones_tenant_id_trd_version_id",
                table: "trd_asignaciones",
                columns: new[] { "tenant_id", "trd_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_trd_asignaciones_trd_version_id_dependencia_org_unit_id_ser",
                table: "trd_asignaciones",
                columns: new[] { "trd_version_id", "dependencia_org_unit_id", "serie_documental_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trd_metadatos_lista_maestra_id",
                table: "trd_metadatos",
                column: "lista_maestra_id");

            migrationBuilder.CreateIndex(
                name: "ix_trd_metadatos_trd_asignacion_id_orden",
                table: "trd_metadatos",
                columns: new[] { "trd_asignacion_id", "orden" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trd_metadatos");

            migrationBuilder.DropTable(
                name: "trd_asignaciones");
        }
    }
}
