using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tronox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentosRq04 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nombre_archivo_original = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    soporte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado_firma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expediente_id = table.Column<long>(type: "bigint", nullable: true),
                    trd_asignacion_id = table.Column<long>(type: "bigint", nullable: true),
                    trd_tipologia_id = table.Column<long>(type: "bigint", nullable: true),
                    nivel_clasificacion_id = table.Column<long>(type: "bigint", nullable: true),
                    fecha_documento = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_incorporacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    orden_en_expediente = table.Column<int>(type: "integer", nullable: true),
                    pagina_inicio = table.Column<int>(type: "integer", nullable: true),
                    pagina_fin = table.Column<int>(type: "integer", nullable: true),
                    folios = table.Column<int>(type: "integer", nullable: true),
                    formato = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tiene_binario = table.Column<bool>(type: "boolean", nullable: false),
                    ruta_almacenamiento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ocr_estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version_actual = table.Column<int>(type: "integer", nullable: false),
                    es_version_historica = table.Column<bool>(type: "boolean", nullable: false),
                    documento_padre_id = table.Column<long>(type: "bigint", nullable: true),
                    justificacion_anulacion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_documentos_documento_padre_id",
                        column: x => x.documento_padre_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_niveles_clasificacion_nivel_clasificacion_id",
                        column: x => x.nivel_clasificacion_id,
                        principalTable: "niveles_clasificacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_trd_asignaciones_trd_asignacion_id",
                        column: x => x.trd_asignacion_id,
                        principalTable: "trd_asignaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_trd_tipologias_trd_tipologia_id",
                        column: x => x.trd_tipologia_id,
                        principalTable: "trd_tipologias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_metadatos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    documento_id = table.Column<long>(type: "bigint", nullable: false),
                    trd_metadato_id = table.Column<long>(type: "bigint", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documento_metadatos", x => x.id);
                    table.ForeignKey(
                        name: "fk_documento_metadatos_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_documento_metadatos_trd_metadatos_trd_metadato_id",
                        column: x => x.trd_metadato_id,
                        principalTable: "trd_metadatos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documento_metadatos_documento_id_trd_metadato_id",
                table: "documento_metadatos",
                columns: new[] { "documento_id", "trd_metadato_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documento_metadatos_trd_metadato_id",
                table: "documento_metadatos",
                column: "trd_metadato_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_documento_padre_id",
                table: "documentos",
                column: "documento_padre_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_expediente_id_orden_en_expediente",
                table: "documentos",
                columns: new[] { "expediente_id", "orden_en_expediente" });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_nivel_clasificacion_id",
                table: "documentos",
                column: "nivel_clasificacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_tenant_id_created_by_estado",
                table: "documentos",
                columns: new[] { "tenant_id", "created_by", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_tenant_id_estado",
                table: "documentos",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_trd_asignacion_id",
                table: "documentos",
                column: "trd_asignacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_trd_tipologia_id",
                table: "documentos",
                column: "trd_tipologia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documento_metadatos");

            migrationBuilder.DropTable(
                name: "documentos");
        }
    }
}
